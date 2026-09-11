using System;
using System.Threading.Tasks;
using CliWrap;
using Spectre.Console;

namespace nugraph;

/// <summary>
/// Runs <c>dotnet-dump</c> commands.
/// </summary>
internal static class DotnetDump
{
    public static async Task RunAsync(IAnsiConsole console)
    {
        console.WriteLine();

        var coreDump = Environment.GetEnvironmentVariable("NUGRAPH_CORE_DUMP")?.ToUpperInvariant() is "1" or "TRUE";
        if (!coreDump)
        {
            console.MarkupLine("Set the [b]NUGRAPH_CORE_DUMP[/] environment variable to [lime]true[/] to capture a core dump on forceful cancellation");
            var versionResult = await Cli.Wrap("dotnet-dump")
                .WithArguments(["--version"])
                .WithValidation(CommandResultValidation.None)
                .ExecuteAsync();

            if (!versionResult.IsSuccess)
            {
                console.MarkupLine("Also install the [b]dotnet-dump[/] tool by running [b]dotnet tool install -g dotnet-dump[/]");
            }

            return;
        }

        var pid = FormattableString.Invariant($"{Environment.ProcessId}");
        var now = FormattableString.Invariant($"{DateTime.Now:yyyyMMdd_HHmmss}");
        var coreFile = OperatingSystem.IsWindows() ? FormattableString.Invariant($"nugraph.dump_{now}.dmp") : FormattableString.Invariant($"nugraph.core_{now}");
        var collectResult = await Cli.Wrap("dotnet-dump")
            .WithArguments([
                "collect",
                "--process-id", pid,
                "--output", coreFile,
                "--type", "heap",
            ])
            .WithStandardOutputPipe(PipeTarget.ToDelegate(console.WriteLine))
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync();

        if (collectResult.IsSuccess)
        {
            await Cli.Wrap("dotnet-dump")
                .WithArguments([
                    "analyze", coreFile,
                    "--command", "clrstack",
                    "--command", "dumpasync",
                    "--command", "exit",
                ])
                .WithStandardOutputPipe(PipeTarget.ToDelegate(console.WriteLine))
                .WithValidation(CommandResultValidation.None)
                .ExecuteAsync();
        }
    }
}