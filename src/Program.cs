using System.CommandLine;
using NuVelocity.Graphics;

namespace NuVelocity.Unpacker;

internal class Program
{
    private static void HandleDecodeCacheSubcommand(
        EncoderFormat format,
        bool useTests,
        bool overrideBlackBlending,
        string input,
        string output)
    {
        ImageExporter exporter = useTests
            ? new ImageExporter(
                format: format,
                dumpRawData: true,
                stripCacheFromPath: false,
                overrideBlackBlending: overrideBlackBlending,
                inputDataFileOrDirectory: "Tests",
                outputFolder: Path.Combine("Tests", "Export"))
            : new ImageExporter(
                format: format,
                dumpRawData: false,
                stripCacheFromPath: true,
                overrideBlackBlending: overrideBlackBlending,
                inputDataFileOrDirectory: input,
                outputFolder: output);

        exporter.ExportData();
    }

    private static void AddDecodeCacheSubcommand(RootCommand root)
    {
        Option<EncoderFormat> formatOption = new(
            name: "--format",
            description: "The format to be used when decoding cache files.",
            getDefaultValue: () => EncoderFormat.Mode3);

        Option<bool> testOption = new(
            name: "--test",
            description: "Use the Tests directory for decoding.",
            getDefaultValue: () => false);

        Option<bool> overrideBlackBlendingOption = new(
            name: "--override-black-blending",
            description: "Override the blended with black and blend black bias blit type with alternative values.",
            getDefaultValue: () => false);

        Option<string> inputOption = new(
            name: "--i",
            description: "Input data archive file or data directory.",
            getDefaultValue: () => "Data");

        Option<string> outputOption = new(
            name: "--o",
            description: "Output directory to store decoded files.",
            getDefaultValue: () => Path.Combine("Data", "Export"));

        Command decodeCacheCommand = new(
            "decode-cache",
            "Decode cached frames and sequences back into TGA images.")
        {
            formatOption,
            testOption,
            overrideBlackBlendingOption,
            inputOption,
            outputOption
        };

        decodeCacheCommand.SetHandler(
            HandleDecodeCacheSubcommand,
            formatOption,
            testOption,
            overrideBlackBlendingOption,
            inputOption,
            outputOption);

        root.AddCommand(decodeCacheCommand);
    }

    private static async Task<int> Main(string[] args)
    {
        RootCommand rootCommand = new(
            "Unpacker for various Velocity Engine file formats.");

        AddDecodeCacheSubcommand(rootCommand);

        return await rootCommand.InvokeAsync(args);
    }
}