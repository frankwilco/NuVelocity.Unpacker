using System.CommandLine;
using NuVelocity.Graphics;

namespace NuVelocity.Unpacker;

internal class Program
{
    private static void HandleRootCommand(EncoderFormat format, bool useTests, bool overrideBlackBlending)
    {
        ImageExporter exporter = new(format, overrideBlackBlending);

        if (useTests)
        {
            exporter.TestFrame();
            exporter.TestSequence();
            return;
        }

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

        var rootCommand = new RootCommand("NuVelocity Unpacker")
        {
            formatOption,
            testOption,
            overrideBlackBlendingOption
        };

        rootCommand.SetHandler(HandleRootCommand,
            formatOption, testOption, overrideBlackBlendingOption);

        return await rootCommand.InvokeAsync(args);
    }
}