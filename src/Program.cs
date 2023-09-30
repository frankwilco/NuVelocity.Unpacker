using SixLabors.ImageSharp.Formats.Tga;
using System.IO;
using NuVelocity.IO;
using System.Diagnostics;

namespace NuVelocity.Unpacker
{
    internal class Program
    {
        private static TgaEncoder TgaEncoder => new()
        {
            BitsPerPixel = TgaBitsPerPixel.Pixel32,
            Compression = TgaCompression.RunLength
        };

        private static void TestFrame()
        {
            var frameFiles = Directory.EnumerateFiles(
                "Tests", "*.Frame", SearchOption.AllDirectories);
            Parallel.ForEach(frameFiles, (file) =>
            {
                Frame frame = Frame.FromStream(
                    out byte[] imageData,
                    out byte[] maskData,
                    File.Open(file, FileMode.Open));
                string target = file.Replace(".Frame", ".");
                Directory.CreateDirectory(Path.GetDirectoryName(target));

                if (imageData != null)
                {
                    File.WriteAllBytes($"{target}_data", imageData);
                }
                if (maskData != null)
                {
                    File.WriteAllBytes($"{target}_rawMask", maskData);
                }
                frame.Texture.Save(target + "tga", TgaEncoder);
                frame.Texture.SaveAsPng(target + "png");
                string logText = $"{file}\n";
                Console.Write(logText);
            });
        }

        private static void TestSequence()
        {
            var sequenceFiles = Directory.EnumerateFiles(
                "Tests", "*.Sequence", SearchOption.AllDirectories);
            Parallel.ForEach(sequenceFiles, (file) =>
            {
                var sequence = Sequence.FromStream(
                    out byte[] lists,
                    out byte[] rawImage,
                    out byte[] maskData,
                    out Image spritesheet,
                    File.Open(file, FileMode.Open));
                string sequenceName = Path.GetFileNameWithoutExtension(file);
                string target = Path.Combine(Path.GetDirectoryName(file), "-" + sequenceName);
                Directory.CreateDirectory(target);

                FileStream propFile = File.Create($"{target}\\Properties.txt");
                PropertySerializer.Serialize(propFile, sequence, sequence.Source);

                if (lists != null)
                {
                    File.WriteAllBytes($"{target}\\_lists", lists);
                }
                if (rawImage != null)
                {
                    File.WriteAllBytes($"{target}\\_rawImage", rawImage);
                }
                if (maskData != null)
                {
                    File.WriteAllBytes($"{target}\\_rawMask", maskData);
                }
                if (spritesheet != null)
                {
                    spritesheet.SaveAsPng($"{target}\\_atlas.png");
                }

                string sequenceSimpleName = sequenceName.Replace(" ", "");
                var images = sequence.Textures;
                for (int i = 0; i < images.Length; i++)
                {
                    images[i].Save($"{target}\\{sequenceSimpleName}{i:0000}.tga", TgaEncoder);
                    images[i].SaveAsPng($"{target}\\{sequenceSimpleName}{i:0000}.png");
                }
                Console.WriteLine($"{file} - {sequence.Source}");
            });
        }

        private static void Main(string[] args)
        {
            //TestFrame();
            //TestSequence();
            //TestSequenceRepack();
            //return;

            if (File.Exists("log.txt"))
            {
                File.Delete("log.txt");
            }

            List<string> logs = new();

            var frameFiles = Directory.EnumerateFiles(
                "Data", "*.Frame", SearchOption.AllDirectories);
            Parallel.ForEach(frameFiles, (file) =>
            {
                string target = file.Replace("\\Cache", "\\Export").Replace(".Frame", ".tga");
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                string propTarget = file.Replace("\\Cache", "").Replace(".Frame", ".txt");

                FileStream frameFile = File.Open(file, FileMode.Open);
                FileStream propFile = null;
                if (File.Exists(propTarget))
                {
                    propFile = File.Open(propTarget, FileMode.Open);
                }
                Frame frame = Frame.FromStream(frameFile, propFile);
                frame.Texture.Save(target, TgaEncoder);

                string logText = $"{file} : {frame.CenterHotSpot}\n";
                logs.Add(logText);
                Console.Write(logText);
            });

            var sequenceFiles = Directory.EnumerateFiles(
                "Data", "*.Sequence", SearchOption.AllDirectories);
            Parallel.ForEach(sequenceFiles, (file) =>
            {
                Sequence sequence = Sequence.FromStream(File.Open(file, FileMode.Open));
                string sequenceName = Path.GetFileNameWithoutExtension(file);
                string sequenceSimpleName = sequenceName.Replace(" ", "");
                string target = Path.Combine(Path.GetDirectoryName(file).Replace("\\Cache", "\\Export"), "-" + sequenceName);
                Directory.CreateDirectory(target);
                var images = sequence.Textures;

                FileStream propertySet = File.Create($"{target}\\Properties.txt");
                PropertySerializer.Serialize(propertySet, sequence, sequence.Source);

                if (images == null)
                {
                    return;
                }
                for (int i = 0; i < images.Length; i++)
                {
                    var image = images[i];
                    image.Save($"{target}\\{sequenceSimpleName}{i:0000}.tga", TgaEncoder);
                }

                string logText = $"{file} : {sequence.CenterHotSpot}, {sequence.Source}\n";
                logs.Add(logText);
                Console.Write(logText);
            });

            File.WriteAllLines("log.txt", logs);

            Console.WriteLine("Done.");
        }
    }
}