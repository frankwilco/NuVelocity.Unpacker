using System.Diagnostics;
using System.Text;
using ICSharpCode.SharpZipLib.Zip;
using NuVelocity.Graphics;
using NuVelocity.Graphics.ImageSharp;
using NuVelocity.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Tga;

namespace NuVelocity.Unpacker;

internal class ImageExporter
{
    private const char kZipDirectorySeparatorChar = '/';
    private const string kZipCacheDirectory = "Cache/";

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

    private void ExportFromFrameStream(
        string sourcePath,
        string exportPath,
        Stream frameStream,
        Stream? propertyListStream,
        string frameName)
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
            string finalExportPath = Path.Combine(exportPath, frameName);
            if (_dumpRawData)
            {
                using Stream dumpPropertyListStream =
                    File.Open($"{finalExportPath}.dmp.txt", FileMode.Create);
                PropertySerializer.Serialize(
                    dumpPropertyListStream, frame, frame.Flags);

                for (int i = 0; i < encoder.LayerCount; i++)
                {
                    byte[]? layer = encoder.LayerData?[i];
                    if (layer == null)
                    {
                        continue;
                    }
                    File.WriteAllBytes($"{finalExportPath}_layer{i}", layer);
                }
            }

            frame.Texture?.Save($"{finalExportPath}.tga", _tgaEncoder);
            logLine += $",{frame.CenterHotSpot},\"{frame.Flags}\"";
        }

        _logger.AppendLine(logLine);
        Console.WriteLine(logLine);
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
            string finalExportPath =
                Path.Combine(exportPath, $"-{sequenceName}")
                + Path.DirectorySeparatorChar;
            Directory.CreateDirectory(finalExportPath);
            if (_dumpRawData)
            {
                if (encoder.ListData != null)
                {
                    File.WriteAllBytes($"{finalExportPath}_lists", encoder.ListData);
                }
                if (encoder.ImageData1 != null)
                {
                    File.WriteAllBytes($"{finalExportPath}_rawImage", encoder.ImageData1);
                }
                if (encoder.ImageData2 != null)
                {
                    File.WriteAllBytes($"{finalExportPath}_rawMask", encoder.ImageData2);
                }
                encoder.Spritesheet?.Save($"{finalExportPath}_atlas.tga", _tgaEncoder);
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

            using Stream propertyListStream =
                File.Create($"{finalExportPath}Properties.txt");
            PropertySerializer.Serialize(
                propertyListStream, sequence, sequence.Flags);

            if (sequence.Textures != null)
            {
                string fileNamePrefix = sequenceName.Replace(" ", "");
                for (int i = 0; i < sequence.Textures.Length; i++)
                {
                    Image image = sequence.Textures[i];
                    image.Save(
                        $"{finalExportPath}{fileNamePrefix}{i:0000}.tga",
                        _tgaEncoder);
                }
            }

            logLine += $",{sequence.CenterHotSpot},\"{sequence.Flags}\"";
        }

        _logger.AppendLine(logLine);
        Console.WriteLine(logLine);
    }

    private void ExportDataFromZip(ZipFile zipFile, ZipEntry zipEntry)
    {
        string filePath = zipEntry.Name;

        bool isFrame = filePath.EndsWith(
            _frameExtension, StringComparison.OrdinalIgnoreCase);
        bool isSequence = filePath.EndsWith(
            _sequenceExtension, StringComparison.OrdinalIgnoreCase);

        if (!isFrame && !isSequence)
        {
            return;
        }

        string parentPath = filePath;
        if (parentPath.StartsWith(kZipCacheDirectory))
        {
            parentPath = parentPath.Replace(
                kZipCacheDirectory, _inputDataDirectory);
        }
        string fileNameWithoutExtension =
            Path.GetFileNameWithoutExtension(filePath);
        string exportPath = Path.Combine(
            _outputDirectory,
            GetDirectoryNameWithFallback(
                parentPath.Replace(
                    kZipDirectorySeparatorChar,
                    Path.DirectorySeparatorChar)));
        Directory.CreateDirectory(exportPath);

        using Stream containerStream = zipFile.GetInputStream(zipEntry);
        if (isFrame)
        {
            string propertyListPath = parentPath.Replace(
                _frameExtension, ".txt");
            ZipEntry? propertyListZipEntry = zipFile.GetEntry(propertyListPath);
            using Stream? propertyListStream = propertyListZipEntry == null
                ? null
                : zipFile.GetInputStream(propertyListZipEntry);
            ExportFromFrameStream(
                filePath,
                exportPath,
                containerStream,
                propertyListStream,
                fileNameWithoutExtension);
            return;
        }
        ExportFromSequenceStream(
            filePath,
            exportPath,
            containerStream,
            fileNameWithoutExtension);
    }

    private void ExportDataFromDirectory(string filePath)
    {
        bool isFrame = filePath.EndsWith(
            _frameExtension, StringComparison.OrdinalIgnoreCase);
        bool isSequence = filePath.EndsWith(
            _sequenceExtension, StringComparison.OrdinalIgnoreCase);

        if (!isFrame && !isSequence)
        {
            return;
        }

        string parentPath = filePath;
        if (_inputDataCacheDirectory != null)
        {
            parentPath = parentPath.Replace(
                _inputDataCacheDirectory, _inputDataDirectory);
        }
        string fileNameWithoutExtension =
            Path.GetFileNameWithoutExtension(filePath);
        string exportPath = GetDirectoryNameWithFallback(
            parentPath.Replace(_inputDataDirectory, _outputDirectory));
        Directory.CreateDirectory(exportPath);

        using Stream containerStream = File.Open(filePath, FileMode.Open);
        if (isFrame)
        {
            Stream? propertyListStream = null;
            string[] framePropertyListSearchPaths = new string[]
            {
                parentPath.Replace(_frameExtension, ".txt"),
                filePath.Replace(_frameExtension, ".txt"),
            };
            foreach (string searchPath in framePropertyListSearchPaths)
            {
                if (File.Exists(searchPath))
                {
                    propertyListStream = File.Open(searchPath, FileMode.Open);
                    break;
                }
            }
            ExportFromFrameStream(
                filePath,
                exportPath,
                containerStream,
                propertyListStream,
                fileNameWithoutExtension);
            propertyListStream?.Dispose();
            return;
        }
        ExportFromSequenceStream(
            filePath,
            exportPath,
            containerStream,
            fileNameWithoutExtension);
    }

    public void ExportData()
    {
        if (File.Exists("log.txt"))
        {
            File.Delete("log.txt");
        }

        _logger.Clear();

        Stopwatch stopwatch = Stopwatch.StartNew();

        if (!string.IsNullOrWhiteSpace(_inputDataDirectory))
        {
            IEnumerable<string> files = Directory.EnumerateFiles(
                _inputDataDirectory, "*.*", SearchOption.AllDirectories);
            Parallel.ForEach(files,
                ExportDataFromDirectory);
        }
        else if (!string.IsNullOrWhiteSpace(_inputDataFile))
        {
            using ZipFile zipFile = new(_inputDataFile);
            Parallel.ForEach(zipFile,
                (filePath) => ExportDataFromZip(zipFile, filePath));
        }
        else
        {
            throw new InvalidOperationException();
        }

        stopwatch.Stop();
        TimeSpan elapsedTime = stopwatch.Elapsed;
        Console.WriteLine("Elapsed time: {0}", elapsedTime);

        File.WriteAllText("log.csv", _logger.ToString());

        Console.WriteLine("Done.");
    }
}