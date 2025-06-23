using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NuGet.Versioning;
using Spectre.Console.Testing;

namespace nugraph.Tests;

public sealed class NugraphProgram : Nugraph
{
    public NugraphProgram()
    {
        var version = typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
        Version = SemanticVersion.Parse(version).ToNormalizedString();
    }

    public override async Task<(int ExitCode, IReadOnlyList<string> StdOut, IReadOnlyList<string> StdErr)> RunAsync(string[] arguments, string? workingDirectory = null)
    {
        using var consoleOut = new TestConsole();
        consoleOut.Profile.Width = 256;
        using var consoleErr = new TestConsole();
        consoleErr.Profile.Width = 256;
        await using var stdOut = new StringWriter();
        var program = new Program(new DirectoryInfo(workingDirectory ?? Environment.CurrentDirectory), consoleOut, consoleErr, stdOut);
        var exitCode = await program.RunAsync(arguments);
        return (exitCode, GetLines(consoleOut, stdOut), GetLines(consoleErr, null));
    }

    public override string Version { get; }

    private static List<string> GetLines(TestConsole console, StringWriter? writer)
    {
        if (console.Output.Length == 0)
            return [];

        var writerLiens = writer?.ToString().ReplaceLineEndings().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries) ?? [];
        var lines =  console.Lines.Concat(writerLiens).ToList();
        return lines;
    }
}