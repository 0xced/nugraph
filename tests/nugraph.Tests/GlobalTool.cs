using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CliWrap;
using CliWrap.Exceptions;
using Xunit;

namespace nugraph.Tests;

public sealed partial class GlobalTool : IAsyncLifetime
{
    private static readonly bool IsContinuousIntegrationBuild = Environment.GetEnvironmentVariable("ContinuousIntegrationBuild") == "true";

    private readonly DirectoryInfo _workingDirectory;
    private string? _previousVersion;

    public GlobalTool()
    {
        _workingDirectory = GetDirectory("tests", "GlobalTool");
        _workingDirectory.Create();
    }

    public string Version { get; private set; } = "N/A";

    public async Task<(int ExitCode, IReadOnlyList<string> StdOut, IReadOnlyList<string> StdErr)> RunAsync(params string[] arguments)
    {
        var stdOut = new List<string>();
        var stdErr = new List<string>();
        var command = Cli.Wrap("nugraph")
            .WithValidation(CommandResultValidation.None)
            .WithArguments(arguments)
            .WithStandardOutputPipe(PipeTarget.ToDelegate(stdOut.Add))
            .WithStandardErrorPipe(PipeTarget.ToDelegate(stdErr.Add));

        var result = await command.ExecuteAsync();

        return (result.ExitCode, stdOut, stdErr);
    }

    async ValueTask IAsyncLifetime.InitializeAsync()
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
            packageFile.MoveTo(GetFullPath(packageName), overwrite: false);
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
        Version = packageVersion.TrimEnd();
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
                TestContext.Current.SendDiagnosticMessage($"==> out: {line}");
            }))
            .WithStandardErrorPipe(PipeTarget.ToDelegate(line =>
            {
                errBuilder.AppendLine(line);
                TestContext.Current.SendDiagnosticMessage($"==> err: {line}");
            }));

        TestContext.Current.SendDiagnosticMessage($"📁 {_workingDirectory.FullName} 🛠️ {command}");

        var result = await command.ExecuteAsync();
        if (result.ExitCode != 0 && !allowNonZeroExitCode)
        {
            throw new CommandExecutionException(command, result.ExitCode, $"Running \"{command}\" failed with exit code {result.ExitCode}{Environment.NewLine}{errBuilder}{outBuilder}".Trim());
        }

        return outBuilder.ToString();
    }

    private static DirectoryInfo GetDirectory(params string[] paths) => new(GetFullPath(paths));

    private static FileInfo GetFile(params string[] paths) => new(GetFullPath(paths));

    private static string GetFullPath(params string[] paths) => Path.GetFullPath(Path.Combine(new[] { GetThisDirectory(), "..", ".." }.Concat(paths).ToArray()));

    private static string GetThisDirectory([CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;

    [GeneratedRegex(@"nugraph\s+(.*?)\s+nugraph")]
    private static partial Regex VersionRegex();
}
