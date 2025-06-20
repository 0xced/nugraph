using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using CliWrap;

namespace nugraph;

/// <summary>
/// Runs <c>dotnet</c> commands.
/// </summary>
internal static partial class DotnetCli
{
    public static async Task<ProjectInfo> RestoreAsync(FileSystemInfo source, CancellationToken cancellationToken)
    {
        return await RestoreAsync(source, allowRetry: true, cancellationToken);
    }

    private static async Task<ProjectInfo> RestoreAsync(FileSystemInfo source, bool allowRetry, CancellationToken cancellationToken)
    {
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        var jsonPipe = new JsonPipeTarget<Result>(SourceGenerationContext.Default.Result);
        var dotnet = Cli.Wrap("dotnet")
            .WithArguments(args =>
            {
                args.Add("restore");
                args.Add(source.FullName);

                // !!! Requires a recent .NET SDK (see https://github.com/dotnet/msbuild/issues/3911)
                // arguments.Add("--target:ResolvePackageAssets"); // may enable if the project is an exe in order to get RuntimeCopyLocalItems + NativeCopyLocalItems
                args.Add($"--getProperty:{nameof(Property.ProjectAssetsFile)}");
                args.Add($"--getProperty:{nameof(Property.TargetFramework)}");
                args.Add($"--getProperty:{nameof(Property.TargetFrameworks)}");
                args.Add($"--getItem:{nameof(Item.RuntimeCopyLocalItems)}");
                args.Add($"--getItem:{nameof(Item.NativeCopyLocalItems)}");
                // Workaround to get ProjectAssetsFile, see https://github.com/dotnet/sdk/issues/49426
                args.Add("--getTargetResult:_LoadRestoreGraphEntryPoints");
            })
            .WithWorkingDirectory(Path.GetDirectoryName(typeof(Program).Assembly.Location) ?? Path.GetTempPath())
            .WithEnvironmentVariables(env => env
                .Set("DOTNET_NOLOGO", "1")
                .Set("DOTNET_CLI_UI_LANGUAGE", "en")
            )
            .WithValidation(CommandResultValidation.None)
            .WithStandardOutputPipe(PipeTarget.Merge(jsonPipe, PipeTarget.ToStringBuilder(stdout)))
            .WithStandardErrorPipe(PipeTarget.ToStringBuilder(stderr));

        var commandResult = await dotnet.ExecuteAsync(forcefulCancellationToken: cancellationToken, gracefulCancellationToken: CancellationToken.None);

        if (!commandResult.IsSuccess)
        {
            var message = stderr.Length > 0 ? stderr.ToString() : stdout.ToString();
            if (message.Contains("MSB1001", StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception("nugraph requires the .NET 8 SDK. Make sure that it's installed and that the global.json file (if any) is configured to use it.");
            }
            throw new Exception($"Running \"{dotnet}\" in \"{dotnet.WorkingDirPath}\" failed with exit code {commandResult.ExitCode}.{Environment.NewLine}{message}");
        }

        var (properties, items) =  jsonPipe.Result ?? throw new Exception($"Running \"{dotnet}\" in \"{dotnet.WorkingDirPath}\" returned a literal 'null' JSON payload");

        if (string.IsNullOrEmpty(properties.ProjectAssetsFile) && allowRetry)
        {
            // If the project was never restored, ProjectAssetsFile may return an empty string. Trying a second time should work.
            return await RestoreAsync(source, allowRetry: false, cancellationToken);
        }

        return new ProjectInfo(properties.GetProjectAssetsFile(), properties.GetTargetFrameworks(), items.GetNuGetPackageIds());
    }

    public sealed record ProjectInfo(FileInfo ProjectAssetsFile, IReadOnlyCollection<string> TargetFrameworks, IReadOnlyCollection<string> CopyLocalPackages);

    [JsonSerializable(typeof(Result))]
    private sealed partial class SourceGenerationContext : JsonSerializerContext;

    private sealed record Result(Property Properties, Item Items);

    private sealed record Property(string? ProjectAssetsFile, string? TargetFramework, string? TargetFrameworks)
    {
        public HashSet<string> GetTargetFrameworks()
        {
            var targetFrameworks = TargetFrameworks?.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet();
            if (targetFrameworks?.Count > 0)
            {
                return targetFrameworks;
            }

            if (TargetFramework != null)
            {
                return [TargetFramework];
            }

            throw new Exception("Either TargetFrameworks or TargetFramework is missing");
        }

        public FileInfo GetProjectAssetsFile()
        {
            return new FileInfo(ProjectAssetsFile ?? throw new Exception("ProjectAssetsFile is missing"));
        }
    }

    private sealed record Item(CopyLocalItem[]? RuntimeCopyLocalItems, CopyLocalItem[]? NativeCopyLocalItems)
    {
        public HashSet<string> GetNuGetPackageIds()
        {
            var runtimeCopyLocalItems = RuntimeCopyLocalItems ?? throw new Exception($"{nameof(RuntimeCopyLocalItems)} is missing");
            var nativeCopyLocalItems = NativeCopyLocalItems ?? throw new Exception($"{nameof(NativeCopyLocalItems)} is missing");
            return runtimeCopyLocalItems.Concat(nativeCopyLocalItems).Select(e => e.NuGetPackageId).OfType<string>().ToHashSet();
        }
    }

    private sealed record CopyLocalItem(string? NuGetPackageId);
}