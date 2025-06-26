using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NuGet.Common;
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

    public override async Task<NugraphResult> RunAsync(string[] arguments, string? workingDirectory = null, LogLevel logLevel = LogLevel.Warning, UrlAction action = UrlAction.print)
    {
        using var consoleOut = new TestConsole();
        consoleOut.Profile.Width = 256;
        using var consoleErr = new TestConsole();
        consoleErr.Profile.Width = 256;
        await using var stdOut = new StringWriter();
        var program = new Program(new ProgramEnvironment(new DirectoryInfo(workingDirectory ?? Environment.CurrentDirectory), consoleOut, consoleErr, stdOut));
        var args = arguments.Append("--log").Append(logLevel.ToString()).Append("--url").Append(action.ToString()).ToArray();
        var exitCode = await program.RunAsync(args);
        return new NugraphResult(exitCode, GetOutput(consoleOut, stdOut), GetOutput(consoleErr, null));
    }

    public override string Version { get; }

    private static string GetOutput(TestConsole console, StringWriter? writer)
    {
        var lines = (console.Output + writer).TrimEnd();
        return lines;
    }
}