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
    public static async Task<ProjectInfo> RestoreAsync(FileSystemInfo source, ILogger logger, CancellationToken cancellationToken)
    {
        return await RestoreAsync(source, allowRetry: true, logger, cancellationToken);
    }

    private static async Task<ProjectInfo> RestoreAsync(FileSystemInfo source, bool allowRetry, ILogger logger, CancellationToken cancellationToken)
    {
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        var jsonPipe = new JsonPipeTarget<Result>(SourceGenerationContext.Default.Result);
        var logPipe = PipeTarget.ToDelegate(logger.LogDebug);
        var dotnet = Cli.Wrap("dotnet")
            .WithArguments(args =>
            {
                args.Add("restore");
                args.Add(source.FullName);

                // Running "dotnet restore" might be extremely slow, even when the project references a single package which is already in the NuGet cache.
                // Sometimes, this log is written to stderr:
                // > MSBuild server unavailable: could not connect to the server within the timeout window; the server may have failed to start. Falling back to an in-process build.
                // Maybe this happens because of dotnet running inside dotnet? Anyway, adding --disable-build-servers prevents the timeout phase (20s) and skips right to the in-process build.
                args.Add("--disable-build-servers");

                // !!! Requires a recent .NET SDK (see https://github.com/dotnet/msbuild/issues/3911)
                args.Add($"--getProperty:{nameof(Property.ProjectAssetsFile)}");
                args.Add($"--getProperty:{nameof(Property.TargetFramework)}");
                args.Add($"--getProperty:{nameof(Property.TargetFrameworks)}");
#if false
                // ResolvePackageAssets only works for non library projects.
                // RuntimeCopyLocalItems + NativeCopyLocalItems can then be used to reduce the dependency graph to packages that have assets which are copied, thus ignoring development dependencies (packages with PrivateAssets="all")
                args.Add("--target:ResolvePackageAssets");
                args.Add($"--getItem:{nameof(Item.RuntimeCopyLocalItems)}");
                args.Add($"--getItem:{nameof(Item.NativeCopyLocalItems)}");
#endif
                // Workaround to get ProjectAssetsFile, see https://github.com/dotnet/sdk/issues/49426
                args.Add("--getTargetResult:_LoadRestoreGraphEntryPoints");
            })
            .WithWorkingDirectory(Path.GetDirectoryName(typeof(Program).Assembly.Location) ?? Path.GetTempPath())
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

        var (properties, items) = jsonPipe.Result ?? throw new InvalidDataException("Missing JSON payload");

        if (string.IsNullOrEmpty(properties.ProjectAssetsFile) && allowRetry)
        {
            // If the project was never restored, ProjectAssetsFile may return an empty string. Trying a second time should work.
            return await RestoreAsync(source, allowRetry: false, logger, cancellationToken);
        }

        return new ProjectInfo(properties.GetProjectAssetsFile(), properties.GetTargetFrameworks(), items?.GetNuGetPackageIds() ?? []);
    }

    public sealed record ProjectInfo(FileInfo ProjectAssetsFile, IReadOnlyCollection<NuGetFramework> TargetFrameworks, IReadOnlyCollection<string> CopyLocalPackages);

    [JsonSerializable(typeof(Result))]
    private sealed partial class SourceGenerationContext : JsonSerializerContext;

    private sealed record Result(Property Properties, Item? Items);

    private sealed record Property(string? ProjectAssetsFile, string? TargetFramework, string? TargetFrameworks)
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

    private sealed record Item(CopyLocalItem[]? RuntimeCopyLocalItems, CopyLocalItem[]? NativeCopyLocalItems)
    {
        public HashSet<string> GetNuGetPackageIds()
        {
            var runtimeCopyLocalItems = RuntimeCopyLocalItems ?? throw new InvalidDataException($"{nameof(RuntimeCopyLocalItems)} is missing");
            var nativeCopyLocalItems = NativeCopyLocalItems ?? throw new InvalidDataException($"{nameof(NativeCopyLocalItems)} is missing");
            return runtimeCopyLocalItems.Concat(nativeCopyLocalItems).Select(e => e.NuGetPackageId).OfType<string>().ToHashSet();
        }
    }

    private sealed record CopyLocalItem(string? NuGetPackageId);
}