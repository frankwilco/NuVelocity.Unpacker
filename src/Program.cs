using SixLabors.ImageSharp.Formats.Tga;
using NuVelocity.Text;
using SixLabors.ImageSharp;
using NuVelocity.Graphics;

namespace NuVelocity.Unpacker;

internal class Program
{
    private static EncoderFormat _encoderFormat = EncoderFormat.Mode3;
    private static string _frameExtension = ".Frame";
    private static string _seqExtension = ".Sequence";

    private static TgaEncoder TgaEncoder => new()
    {
        BitsPerPixel = TgaBitsPerPixel.Pixel32,
        Compression = TgaCompression.RunLength
    };

    private static string GetDirectoryNameWithFallback(string? path)
    {
        string? directoryName = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(directoryName))
        {
            return DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        }
        return directoryName;
    }

    private static void TestFrame()
    {
        var frameFiles = Directory.EnumerateFiles(
            "Tests", "*" + _frameExtension, SearchOption.AllDirectories);
        Parallel.ForEach(frameFiles, (file) =>
        {
            string target = file.Replace(_frameExtension, ".");

            string propTarget = target + "txt";
            FileStream? propFile = null;
            if (File.Exists(propTarget))
            {
                propFile = File.Open(propTarget, FileMode.Open);
            }

            SlisFrameEncoder encoder = new(
                _encoderFormat,
                File.Open(file, FileMode.Open),
                propFile);
            SlisFrame frame = encoder.SlisFrame;

            PropertySerializer.Serialize(
                File.Open(target + "txt", FileMode.Create),
                frame,
                frame.Flags);

            for (int i = 0; i < encoder.LayerCount; i++)
            {
                byte[]? layer = encoder.LayerData?[i];
                if (layer == null)
                {
                    continue;
                }
                File.WriteAllBytes($"{target}_layer{i}", layer);
            }

            frame?.Texture?.Save(target + "tga", TgaEncoder);
            frame?.Texture?.SaveAsPng(target + "png");
            string logText = $"{file}\n";
            Console.Write(logText);
        });
    }

    private static void TestSequence()
    {
        var sequenceFiles = Directory.EnumerateFiles(
            "Tests", "*" + _seqExtension, SearchOption.AllDirectories);
        Parallel.ForEach(sequenceFiles, (file) =>
        {
            SlisSequenceEncoder encoder = new(
                _encoderFormat,
                File.Open(file, FileMode.Open),
                null);
            SlisSequence sequence = encoder.SlisSequence;

            string sequenceName = Path.GetFileNameWithoutExtension(file);
            string target = Path.Combine(
                GetDirectoryNameWithFallback(file),
                $"-{sequenceName}");
            Directory.CreateDirectory(target);

            FileStream propFile = File.Create($"{target}\\Properties.txt");
            PropertySerializer.Serialize(propFile, sequence, sequence.Flags);

            if (encoder.ListData != null)
            {
                File.WriteAllBytes($"{target}\\_lists", encoder.ListData);
            }
            if (encoder.ImageData1 != null)
            {
                File.WriteAllBytes($"{target}\\_rawImage", encoder.ImageData1);
            }
            if (encoder.ImageData2 != null)
            {
                File.WriteAllBytes($"{target}\\_rawMask", encoder.ImageData2);
            }
            encoder.Spritesheet?.SaveAsPng($"{target}\\_atlas.png");

            string sequenceSimpleName = sequenceName.Replace(" ", "");
            Image[]? images = sequence.Textures;
            if (images != null)
            {
                for (int i = 0; i < images.Length; i++)
                {
                    images[i].Save($"{target}\\{sequenceSimpleName}{i:0000}.tga", TgaEncoder);
                    images[i].SaveAsPng($"{target}\\{sequenceSimpleName}{i:0000}.png");
                }
            }
            Console.WriteLine($"{file} - {sequence.Flags}");
        });
    }

    private static void Main(string[] args)
    {
        if (File.Exists("2.txt"))
        {
            _encoderFormat = EncoderFormat.Mode2;
            _frameExtension = ".frm16";
            _seqExtension = ".seq16";
        }
        
        if (File.Exists("1test.txt"))
        {
            TestFrame();
            TestSequence();
            return;
        }

        if (File.Exists("log.txt"))
        {
            File.Delete("log.txt");
        }

        List<string> logs = new();

        var frameFiles = Directory.EnumerateFiles(
            "Data", "*" + _frameExtension, SearchOption.AllDirectories);
        Parallel.ForEach(frameFiles, (file) =>
        {
            string target = file.Replace("\\Cache", "\\Export").Replace(_frameExtension, ".tga");
            Directory.CreateDirectory(GetDirectoryNameWithFallback(target));
            string propTarget = file.Replace("\\Cache", "").Replace(_frameExtension, ".txt");

            FileStream frameFile = File.Open(file, FileMode.Open);
            FileStream? propFile = null;
            if (File.Exists(propTarget))
            {
                propFile = File.Open(propTarget, FileMode.Open);
            }
            SlisFrameEncoder encoder = new(_encoderFormat, frameFile, propFile);
            SlisFrame frame = encoder.SlisFrame;
            string logText = file;
            if (frame == null)
            {
                logText += $" : FAIL\n";
            }
            else
            {
                frame?.Texture?.Save(target, TgaEncoder);
                logText += $" : {frame?.CenterHotSpot}, {frame?.Flags}\n";
            }
            logs.Add(logText);
            Console.Write(logText);
        });

        var sequenceFiles = Directory.EnumerateFiles(
            "Data", "*" + _seqExtension, SearchOption.AllDirectories);
        Parallel.ForEach(sequenceFiles, (file) =>
        {
            SlisSequenceEncoder encoder = new(
                _encoderFormat,
                File.Open(file, FileMode.Open),
                null);
            SlisSequence sequence = encoder.SlisSequence;
            string sequenceName = Path.GetFileNameWithoutExtension(file);
            string sequenceSimpleName = sequenceName.Replace(" ", "");
            string target = Path.Combine(
                GetDirectoryNameWithFallback(file).Replace(
                    "\\Cache", "\\Export"),
                "-" + sequenceName);
            Directory.CreateDirectory(target);
            var images = sequence.Textures;

            // XXX: override blended with black property and blit type if
            // it uses black biased blitting (which we don't support yet).
            sequence.BlendedWithBlack = false;
            if (sequence.BlitType == BlitType.BlendBlackBias)
            {
                sequence.BlitType = BlitType.TransparentMask;
            }

            FileStream propertySet = File.Create($"{target}\\Properties.txt");
            PropertySerializer.Serialize(propertySet, sequence, sequence.Flags);

            if (images == null)
            {
                return;
            }
            for (int i = 0; i < images.Length; i++)
            {
                var image = images[i];
                image.Save($"{target}\\{sequenceSimpleName}{i:0000}.tga", TgaEncoder);
            }

            string logText = $"{file} : {sequence.CenterHotSpot}, {sequence.Flags}\n";
            logs.Add(logText);
            Console.Write(logText);
        });

        File.WriteAllLines("log.txt", logs);

        Console.WriteLine("Done.");
    }
}