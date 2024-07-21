using System.CommandLine;
using NuVelocity.Graphics;

namespace NuVelocity.Unpacker;

internal class Program
{
    private static void HandleRootCommand(
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

    private static async Task<int> Main(string[] args)
    {
        var formatOption = new Option<EncoderFormat>(
            name: "--format",
            description: "The format to be used when decoding cache files.",
            getDefaultValue: () => EncoderFormat.Mode3);

        var testOption = new Option<bool>(
            name: "--test",
            description: "Use the Tests directory for decoding.",
            getDefaultValue: () => false);

        var overrideBlackBlendingOption = new Option<bool>(
            name: "--override-black-blending",
            description: "Override the blended with black and blend black bias blit type with alternative values.",
            getDefaultValue: () => false);

        var inputOption = new Option<string>(
            name: "--i",
            description: "Input data archive file or data directory.",
            getDefaultValue: () => "Data");

        var outputOption = new Option<string>(
            name: "--o",
            description: "Output directory to store decoded files.",
            getDefaultValue: () => Path.Combine("Data", "Export"));

        var rootCommand = new RootCommand("NuVelocity Unpacker")
        {
            formatOption,
            testOption,
            overrideBlackBlendingOption,
            inputOption,
            outputOption
        };

        rootCommand.SetHandler(
            HandleRootCommand,
            formatOption,
            testOption,
            overrideBlackBlendingOption,
            inputOption,
            outputOption);

        return await rootCommand.InvokeAsync(args);
    }
}