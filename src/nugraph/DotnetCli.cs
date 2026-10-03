using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using CliWrap;
using NuGet.Common;
using NuGet.Frameworks;

namespace nugraph;

/// <summary>
/// Runs <c>dotnet</c> commands.
/// </summary>
internal static partial class DotnetCli
{
    public static async Task<ProjectInfo> RestoreAsync(FileSystemInfo source, IReadOnlyList<string> additionalRestoreArgs, ILogger logger, CancellationToken cancellationToken)
    {
        var jsonPipe = new JsonPipeTarget<RestoreResult>(SourceGenerationContext.Default.RestoreResult);
        var result = await RestoreAsync(jsonPipe, source, additionalRestoreArgs, logger, cancellationToken);

        if (string.IsNullOrEmpty(result.GetProperties().ProjectAssetsFile))
        {
            // If a multi-targeted project was never restored, ProjectAssetsFile may return an empty string.
            // Trying a second time should work, see https://github.com/dotnet/sdk/issues/49426#issuecomment-2988833653
            result = await RestoreAsync(jsonPipe, source, additionalRestoreArgs, logger, cancellationToken);
        }

        var properties = result.GetProperties();
        return new ProjectInfo(properties.GetProjectAssetsFile(), properties.GetTargetFrameworks());
    }

    public static async Task<IReadOnlySet<NuGetFramework>> GetSupportedFrameworksAsync(DirectoryInfo? sdk, IReadOnlyList<string> additionalRestoreArgs, ILogger logger, CancellationToken cancellationToken)
    {
        using var emptyProject = new TemporaryProject(FrameworkConstants.CommonFrameworks.NetStandard20, sdk);

        var jsonPipe = new JsonPipeTarget<SupportedFrameworkResult>(SourceGenerationContext.Default.SupportedFrameworkResult);
        var result = await RestoreAsync(jsonPipe, emptyProject.File, additionalRestoreArgs, logger, cancellationToken);

        return result.GetItems().GetSupportedTargetFrameworks();
    }

    private static async Task<T> RestoreAsync<T>(JsonPipeTarget<T> jsonPipe, FileSystemInfo source, IReadOnlyList<string> additionalRestoreArgs, ILogger logger, CancellationToken cancellationToken)
    {
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        var logPipe = PipeTarget.ToDelegate(logger.LogDebug);
        var dotnet = Cli.Wrap("dotnet")
            .WithArguments(args =>
            {
                args.Add("restore");
                args.Add(source.FullName);

                // !!! --getProperty and --getItem require a recent .NET SDK (see https://github.com/dotnet/msbuild/issues/3911)
                if (typeof(T) == typeof(RestoreResult))
                {
                    args.Add($"--getProperty:{nameof(RestoreProperty.ProjectAssetsFile)}");
                    args.Add($"--getProperty:{nameof(RestoreProperty.TargetFramework)}");
                    args.Add($"--getProperty:{nameof(RestoreProperty.TargetFrameworks)}");

                    // Workaround to get ProjectAssetsFile, see https://github.com/dotnet/sdk/issues/49426
                    args.Add("--getTargetResult:_LoadRestoreGraphEntryPoints");
                }
                else if (typeof(T) == typeof(SupportedFrameworkResult))
                {
                    args.Add($"--getItem:{nameof(SupportedFrameworkItem.SupportedTargetFramework)}");
                }

                foreach (var arg in additionalRestoreArgs)
                {
                    args.Add(arg);
                }
            })
            .WithWorkingDirectory(source is FileInfo { DirectoryName: not null } file ? file.DirectoryName : Path.GetDirectoryName(typeof(Program).Assembly.Location) ?? Path.GetTempPath())
            .WithEnvironmentVariables(env => env
                .Set("DOTNET_NOLOGO", "1")
                .Set("DOTNET_CLI_UI_LANGUAGE", "en")
            )
            .WithValidation(CommandResultValidation.None)
            .WithStandardOutputPipe(PipeTarget.Merge(jsonPipe, PipeTarget.ToStringBuilder(stdout), logPipe))
            .WithStandardErrorPipe(PipeTarget.Merge(PipeTarget.ToStringBuilder(stderr), logPipe));

        logger.LogVerbose($"Working directory: {dotnet.WorkingDirPath}");
        logger.LogVerbose(dotnet.ToString());
        var stopwatch = Stopwatch.StartNew();
        var commandResult = await dotnet.ExecuteAsync(forcefulCancellationToken: cancellationToken, gracefulCancellationToken: CancellationToken.None);
        logger.LogVerbose($"Restored in {stopwatch.Elapsed.TotalSeconds:N1} seconds");

        if (!commandResult.IsSuccess)
        {
            var output = stderr.Length > 0 ? stderr.ToString() : stdout.ToString();
            throw RestoreException.Create(exitCode: commandResult.ExitCode, workingDirectory: dotnet.WorkingDirPath, command: dotnet.ToString(), output: output);
        }

        return jsonPipe.Result ?? throw new InvalidDataException("Missing JSON payload");
    }

    public sealed record ProjectInfo(FileInfo ProjectAssetsFile, IReadOnlyCollection<NuGetFramework> TargetFrameworks);

    [JsonSerializable(typeof(RestoreResult))]
    [JsonSerializable(typeof(SupportedFrameworkResult))]
    private sealed partial class SourceGenerationContext : JsonSerializerContext;

    private sealed record RestoreResult(RestoreProperty? Properties)
    {
        public RestoreProperty GetProperties()
        {
            return Properties ?? throw new InvalidDataException($"{nameof(Properties)} is missing");
        }
    }

    private sealed record SupportedFrameworkResult(SupportedFrameworkItem? Items)
    {
        public SupportedFrameworkItem GetItems()
        {
            return Items ?? throw new InvalidDataException($"{nameof(Items)} is missing");
        }
    }

    private sealed record RestoreProperty(string? ProjectAssetsFile, string? TargetFramework, string? TargetFrameworks)
    {
        public HashSet<NuGetFramework> GetTargetFrameworks()
        {
            var targetFrameworks = TargetFrameworks?.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(NuGetFramework.Parse).ToHashSet();
            if (targetFrameworks?.Count > 0)
            {
                return targetFrameworks;
            }

            if (!string.IsNullOrEmpty(TargetFramework))
            {
                return [NuGetFramework.Parse(TargetFramework)];
            }

            throw new InvalidDataException($"Either {nameof(TargetFrameworks)} (plural) or {nameof(TargetFramework)} (singular) is missing");
        }

        public FileInfo GetProjectAssetsFile()
        {
            return new FileInfo(ProjectAssetsFile ?? throw new InvalidDataException($"{nameof(ProjectAssetsFile)} is missing"));
        }
    }

    private sealed record SupportedFrameworkItem(Tfm[]? SupportedTargetFramework)
    {
        public HashSet<NuGetFramework> GetSupportedTargetFrameworks()
        {
            var supportedTargetFramework = SupportedTargetFramework ?? throw new InvalidDataException($"{nameof(SupportedTargetFramework)} is missing");
            return [.. supportedTargetFramework.Select(e => e.GetIdentity()).Select(NuGetFramework.Parse)];
        }
    }

    private sealed record Tfm(string? Identity)
    {
        public string GetIdentity()
        {
            return Identity ?? throw new InvalidDataException($"{nameof(Identity)} is missing");
        }
    }
}