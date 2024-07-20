using System.Diagnostics.CodeAnalysis;
using System.Text;
using NuVelocity.Graphics;
using NuVelocity.Graphics.ImageSharp;
using NuVelocity.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Tga;

namespace NuVelocity.Unpacker;

internal class ImageExporter
{
    private readonly EncoderFormat _encoderFormat;
    private readonly string _frameExtension;
    private readonly string _sequenceExtension;
    private readonly bool _overrideBlackBlending;
    private readonly bool _dumpRawData;
    private readonly string _inputDataFile;
    private readonly string _inputDataDirectory;
    private readonly string? _inputDataCacheDirectory;
    private readonly string _outputDirectory;
    private readonly TgaEncoder _tgaEncoder;
    private readonly StringBuilder _logger;

    public ImageExporter(
        EncoderFormat format,
        bool dumpRawData,
        bool stripCacheFromPath,
        bool overrideBlackBlending,
        string inputDataFileOrDirectory,
        string outputFolder)
    {
        switch (format)
        {
            case EncoderFormat.Mode1:
                _encoderFormat = EncoderFormat.Mode1;
                _frameExtension = ".frm";
                _sequenceExtension = ".seq";
                break;
            case EncoderFormat.Mode2:
                _encoderFormat = EncoderFormat.Mode2;
                _frameExtension = ".frm16";
                _sequenceExtension = ".seq16";
                break;
            case EncoderFormat.Mode3:
                _encoderFormat = EncoderFormat.Mode3;
                _frameExtension = ".Frame";
                _sequenceExtension = ".Sequence";
                break;
            default:
                throw new NotSupportedException();
        }

        _tgaEncoder = new()
        {
            BitsPerPixel = TgaBitsPerPixel.Pixel32,
            Compression = TgaCompression.RunLength
        };

        _inputDataDirectory = "";
        _inputDataFile = "";

        if (Directory.Exists(inputDataFileOrDirectory))
        {
            _inputDataDirectory = Path.GetFullPath(inputDataFileOrDirectory);
            if (stripCacheFromPath)
            {
                _inputDataCacheDirectory = Path.Combine(_inputDataDirectory, "Cache");
            }
        }
        else if (File.Exists(inputDataFileOrDirectory))
        {
            _inputDataFile = Path.GetFullPath(inputDataFileOrDirectory);
        }
        else
        {
            throw new ArgumentException(
                "Input data file or directory does not exist.",
                nameof(inputDataFileOrDirectory));
        }

        _dumpRawData = dumpRawData;
        _overrideBlackBlending = overrideBlackBlending;
        _outputDirectory = Path.GetFullPath(outputFolder);
        _logger = new();
    }

    private static string GetDirectoryNameWithFallback(string? path)
    {
        string? directoryName = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(directoryName))
        {
            return DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        }
        return directoryName;
    }

    private void ExportFromFrameFile(string file)
    {
        string parentPath = file;
        if (_inputDataCacheDirectory != null)
        {
            parentPath = parentPath.Replace(_inputDataCacheDirectory, _inputDataDirectory);
        }
        string exportPath = parentPath.Replace(_inputDataDirectory, _outputDirectory);
        Directory.CreateDirectory(GetDirectoryNameWithFallback(exportPath));
        exportPath = exportPath.Replace(_frameExtension, "");

        Stream? propertyListFile = null;
        string[] framePropertyListSearchPaths = new string[]
        {
            parentPath.Replace(_frameExtension, ".txt"),
            file.Replace(_frameExtension, ".txt"),
            exportPath + ".txt"
        };
        foreach (string searchPath in framePropertyListSearchPaths)
        {
            if (File.Exists(searchPath))
            {
                propertyListFile = File.Open(searchPath, FileMode.Open);
                break;
            }
        }

        Stream frameFile = File.Open(file, FileMode.Open);
        ExportFromFrameStream(file, exportPath, frameFile, propertyListFile);
    }

    private void ExportFromFrameStream(
        string sourcePath,
        string exportPath,
        Stream frameStream,
        Stream? propertyListStream)
    {
        string logLine = sourcePath;
        SlisFrameEncoder? encoder = null;
        try
        {
            encoder = new(_encoderFormat, frameStream, propertyListStream);
            logLine += ",1";
        }
        catch (NotImplementedException)
        {
            logLine += ",0";
        }

        SlisFrame? frame = encoder?.SlisFrame;
        if (encoder == null || frame == null)
        {
            logLine += ",fail,fail";
        }
        else
        {
            if (_dumpRawData)
            {
                PropertySerializer.Serialize(
                    File.Open($"{exportPath}.dmp.txt", FileMode.Create),
                    frame,
                    frame.Flags);

                for (int i = 0; i < encoder.LayerCount; i++)
                {
                    byte[]? layer = encoder.LayerData?[i];
                    if (layer == null)
                    {
                        continue;
                    }
                    File.WriteAllBytes($"{exportPath}_layer{i}", layer);
                }
            }

            frame.Texture?.Save($"{exportPath}.tga", _tgaEncoder);
            logLine += $",{frame.CenterHotSpot},\"{frame.Flags}\"";
        }

        _logger.AppendLine(logLine);
        Console.WriteLine(logLine);
    }

    private void ExportFromSequenceFile(string file)
    {
        string sequenceName = Path.GetFileNameWithoutExtension(file);
        string parentPath = file;
        if (_inputDataCacheDirectory != null)
        {
            parentPath = parentPath.Replace(_inputDataCacheDirectory, _inputDataDirectory);
        }
        string exportPath = Path.Combine(
            GetDirectoryNameWithFallback(parentPath)
                .Replace(_inputDataDirectory, _outputDirectory),
            "-" + sequenceName);
        Directory.CreateDirectory(exportPath);
        FileStream sequenceStream = File.Open(file, FileMode.Open);
        ExportFromSequenceStream(file, exportPath, sequenceStream, sequenceName.Replace(" ", ""));
    }

    private void ExportFromSequenceStream(
        string sourcePath,
        string exportPath,
        Stream sequenceStream,
        string sequenceName)
    {
        string logLine = sourcePath;
        SlisSequenceEncoder? encoder = null;
        try
        {
            encoder = new(
                _encoderFormat,
                sequenceStream,
                null);
            logLine += ",1";
        }
        catch (NotImplementedException)
        {
            logLine += ",0";
        }

        if (encoder == null || encoder.Sequence == null)
        {
            logLine += ",fail,fail";
        }
        else
        {
            if (_dumpRawData)
            {
                if (encoder.ListData != null)
                {
                    File.WriteAllBytes($"{exportPath}\\_lists", encoder.ListData);
                }
                if (encoder.ImageData1 != null)
                {
                    File.WriteAllBytes($"{exportPath}\\_rawImage", encoder.ImageData1);
                }
                if (encoder.ImageData2 != null)
                {
                    File.WriteAllBytes($"{exportPath}\\_rawMask", encoder.ImageData2);
                }
                encoder.Spritesheet?.Save($"{exportPath}\\_atlas.tga", _tgaEncoder);
            }

            SlisSequence sequence = encoder.SlisSequence;

            // XXX: override blended with black property and blit type if
            // it uses black biased blitting (which we don't support yet).
            if (_overrideBlackBlending)
            {
                sequence.BlendedWithBlack = false;
                if (sequence.BlitType == BlitType.BlendBlackBias)
                {
                    sequence.BlitType = BlitType.TransparentMask;
                }
            }

            FileStream propertySet = File.Create($"{exportPath}\\Properties.txt");
            PropertySerializer.Serialize(propertySet, sequence, sequence.Flags);

            if (sequence.Textures != null)
            {
                for (int i = 0; i < sequence.Textures.Length; i++)
                {
                    Image image = sequence.Textures[i];
                    image.Save(
                        $"{exportPath}\\{sequenceName}{i:0000}.tga",
                        _tgaEncoder);
                }
            }

            logLine += $",{sequence.CenterHotSpot},\"{sequence.Flags}\"";
        }

        _logger.AppendLine(logLine);
        Console.WriteLine(logLine);
    }

    public void ExportData()
    {
        if (File.Exists("log.txt"))
        {
            File.Delete("log.txt");
        }

        _logger.Clear();

        if (!string.IsNullOrWhiteSpace(_inputDataDirectory))
        {
            IEnumerable<string> frameFiles = Directory.EnumerateFiles(
                _inputDataDirectory,
                "*" + _frameExtension,
               SearchOption.AllDirectories);
            Parallel.ForEach(frameFiles, ExportFromFrameFile);

            IEnumerable<string> sequenceFiles = Directory.EnumerateFiles(
                _inputDataDirectory,
                "*" + _sequenceExtension,
                SearchOption.AllDirectories);
            Parallel.ForEach(sequenceFiles, ExportFromSequenceFile);
        }
        else
        {
            throw new NotImplementedException();
        }

        File.WriteAllText("log.csv", _logger.ToString());

        Console.WriteLine("Done.");
    }
}