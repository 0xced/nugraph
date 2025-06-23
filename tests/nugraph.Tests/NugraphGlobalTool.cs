using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CliWrap;
using CliWrap.Exceptions;
using TUnit.Core.Interfaces;
using static nugraph.Tests.RepositoryDirectories;

namespace nugraph.Tests;

public sealed partial class NugraphGlobalTool : Nugraph, IAsyncInitializer, IAsyncDisposable
{
    private static readonly bool IsContinuousIntegrationBuild = Environment.GetEnvironmentVariable("ContinuousIntegrationBuild") == "true";

    private readonly DirectoryInfo _workingDirectory;
    private string? _version;
    private string? _previousVersion;

    public NugraphGlobalTool()
    {
        _workingDirectory = GetDirectory("tests", "GlobalTool");
        _workingDirectory.Create();
    }

    public override string Version => _version ?? "N/A";

    public override async Task<(int ExitCode, IReadOnlyList<string> StdOut, IReadOnlyList<string> StdErr)> RunAsync(string[] arguments, string? workingDirectory = null)
    {
        var stdOut = new List<string>();
        var stdErr = new List<string>();
        var command = Cli.Wrap("nugraph")
            .WithValidation(CommandResultValidation.None)
            .WithArguments(arguments)
            .WithWorkingDirectory(workingDirectory ?? Environment.CurrentDirectory)
            .WithStandardOutputPipe(PipeTarget.ToDelegate(stdOut.Add))
            .WithStandardErrorPipe(PipeTarget.ToDelegate(stdErr.Add));

        var result = await command.ExecuteAsync();

        return (result.ExitCode, stdOut, stdErr);
    }

    async Task IAsyncInitializer.InitializeAsync()
    {
        await PackAsync();
        await InstallAsync();
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        await RunDotnetAsync(["tool", "uninstall", "--global", "nugraph"]);
        if (_previousVersion != null)
        {
            await RunDotnetAsync(["tool", "install", "--global", "nugraph", "--version", _previousVersion]);
        }

        if (IsContinuousIntegrationBuild)
        {
            // The NuGet package was already built as part of the tests (PackAsync),
            // so move it to the root of the repository for the "Upload NuGet package artifact" step to pick it.
            var packageName = $"nugraph.{Version}.nupkg";
            var packageFile = _workingDirectory.File(packageName);
            packageFile.MoveTo(GetPath(packageName), overwrite: false);
        }

        _workingDirectory.Delete(recursive: true);
    }

    private async Task PackAsync()
    {
        var projectFile = GetFile("src", "nugraph", "nugraph.csproj");
        var packArgs = new List<string> {
            "pack", projectFile.FullName,
            "--output", _workingDirectory.FullName,
            "--getProperty:PackageVersion",
        };
        if (IsContinuousIntegrationBuild)
        {
            packArgs.Add("--no-build");
        }
        var packageVersion = await RunDotnetAsync(packArgs.ToArray());
        _version = packageVersion.TrimEnd();
    }

    private async Task InstallAsync()
    {
        var installed = await RunDotnetAsync(["tool", "list", "--global", "nugraph"], allowNonZeroExitCode: true);
        var match = VersionRegex().Match(installed);
        if (match.Success)
        {
            _previousVersion = match.Groups[1].Value;
        }

        var installArgs = new[] {
            "tool",
            "install",
            "nugraph",
            "--global",
            "--version", Version,
            "--add-source", _workingDirectory.FullName,
        };
        await RunDotnetAsync(installArgs);
    }

    private async Task<string> RunDotnetAsync(string[] arguments, bool allowNonZeroExitCode = false)
    {
        var outBuilder = new StringBuilder();
        var errBuilder = new StringBuilder();
        var command = Cli.Wrap("dotnet")
            .WithValidation(CommandResultValidation.None)
            .WithWorkingDirectory(_workingDirectory.FullName)
            .WithEnvironmentVariables(env => env.Set("DOTNET_NOLOGO", "1"))
            .WithArguments(arguments)
            .WithStandardOutputPipe(PipeTarget.ToDelegate(line =>
            {
                outBuilder.AppendLine(line);
                TestContext.Current?.OutputWriter.WriteLine($"==> out: {line}");
            }))
            .WithStandardErrorPipe(PipeTarget.ToDelegate(line =>
            {
                errBuilder.AppendLine(line);
                TestContext.Current?.OutputWriter.WriteLine($"==> err: {line}");
            }));

        TestContext.Current?.OutputWriter.WriteLine($"📁 {_workingDirectory.FullName} 🛠️ {command}");

        var result = await command.ExecuteAsync();
        if (result.ExitCode != 0 && !allowNonZeroExitCode)
        {
            throw new CommandExecutionException(command, result.ExitCode, $"Running \"{command}\" failed with exit code {result.ExitCode}{Environment.NewLine}{errBuilder}{outBuilder}".Trim());
        }

        return outBuilder.ToString();
    }

    [GeneratedRegex(@"nugraph\s+(.*?)\s+nugraph")]
    private static partial Regex VersionRegex();
}
