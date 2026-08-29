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
        var (properties, items) = await RestoreAsync(jsonPipe, source, additionalRestoreArgs, logger, cancellationToken);

        if (string.IsNullOrEmpty(properties.ProjectAssetsFile))
        {
            // If the project was never restored, ProjectAssetsFile may return an empty string. Trying a second time should work.
            (properties, items) = await RestoreAsync(jsonPipe, source, additionalRestoreArgs, logger, cancellationToken);
        }

        return new ProjectInfo(properties.GetProjectAssetsFile(), properties.GetTargetFrameworks(), items?.GetNuGetPackageIds() ?? []);
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
#if false
                    // ResolvePackageAssets only works for non-library projects.
                    // RuntimeCopyLocalItems + NativeCopyLocalItems can then be used to reduce the dependency graph to packages that have assets which are copied, thus ignoring development dependencies (packages with PrivateAssets="all")
                    args.Add("--target:ResolvePackageAssets");
                    args.Add($"--getItem:{nameof(Item.RuntimeCopyLocalItems)}");
                    args.Add($"--getItem:{nameof(Item.NativeCopyLocalItems)}");
#endif
                    // Workaround to get ProjectAssetsFile, see https://github.com/dotnet/sdk/issues/49426
                    args.Add("--getTargetResult:_LoadRestoreGraphEntryPoints");
                }
                else if (typeof(T) == typeof(SupportedFrameworkResult))
                {
                    args.Add($"--getItem:{nameof(RestoreItem.SupportedTargetFramework)}");
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

    public sealed record ProjectInfo(FileInfo ProjectAssetsFile, IReadOnlyCollection<NuGetFramework> TargetFrameworks, IReadOnlyCollection<string> CopyLocalPackages);

    [JsonSerializable(typeof(RestoreResult))]
    [JsonSerializable(typeof(SupportedFrameworkResult))]
    private sealed partial class SourceGenerationContext : JsonSerializerContext;

    private sealed record RestoreResult(RestoreProperty? Properties, RestoreItem? Items)
    {
        public void Deconstruct(out RestoreProperty properties, out RestoreItem? items)
        {
            properties = Properties ?? throw new InvalidDataException($"{nameof(Properties)} is missing");
            items = Items;
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

    private sealed record RestoreItem(CopyLocalItem[]? RuntimeCopyLocalItems, CopyLocalItem[]? NativeCopyLocalItems, Tfm[]? SupportedTargetFramework)
    {
        public HashSet<string> GetNuGetPackageIds()
        {
            var runtimeCopyLocalItems = RuntimeCopyLocalItems ?? throw new InvalidDataException($"{nameof(RuntimeCopyLocalItems)} is missing");
            var nativeCopyLocalItems = NativeCopyLocalItems ?? throw new InvalidDataException($"{nameof(NativeCopyLocalItems)} is missing");
            return [.. runtimeCopyLocalItems.Concat(nativeCopyLocalItems).Select(e => e.NuGetPackageId).OfType<string>()];
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

    private sealed record CopyLocalItem(string? NuGetPackageId);

    private sealed record Tfm(string? Identity)
    {
        public string GetIdentity()
        {
            return Identity ?? throw new InvalidDataException($"{nameof(Identity)} is missing");
        }
    }
}