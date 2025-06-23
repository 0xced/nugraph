using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Versioning;
using Spectre.Console;
using Spectre.Console.Testing;

namespace nugraph.Tests;

public sealed class NugraphProgram : Nugraph
{
    public NugraphProgram()
    {
        var version = typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
        Version = SemanticVersion.Parse(version).ToNormalizedString();
    }

    public override async Task<(int ExitCode, string StdOut, string StdErr)> RunAsync(string[] arguments, string? workingDirectory = null, LogLevel logLevel = LogLevel.Warning, bool noBrowser = true)
    {
        var consoleOut = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.Detect,
            Out = new AnsiConsoleOutput(Console.Out),
        });
        var consoleErr = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.Detect,
            Out = new AnsiConsoleOutput(Console.Error),
        });
        // using var consoleOut = new TestConsole();
        // consoleOut.Profile.Width = 256;
        // using var consoleErr = new TestConsole();
        // consoleErr.Profile.Width = 256;
        await using var stdOut = new StringWriter();
        var program = new Program(new DirectoryInfo(workingDirectory ?? Environment.CurrentDirectory), consoleOut, consoleErr, stdOut);
        var args = arguments.Append("--log").Append(logLevel.ToString()).Append("--no-browser").Append(noBrowser.ToString()).ToArray();
        var exitCode = await program.RunAsync(args);
        return (exitCode, GetOutput(consoleOut, stdOut), GetOutput(consoleErr, null));
    }

    public override string Version { get; }

    private static string GetOutput(IAnsiConsole console, StringWriter? writer)
    {
        if (console is TestConsole testConsole)
        {
            var lines = (testConsole.Output + writer).TrimEnd();
            return lines;
        }

        return "";
    }
}