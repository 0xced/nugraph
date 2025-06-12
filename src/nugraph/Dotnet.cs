using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using CliWrap;
using CliWrap.Builders;
using NuGet.Frameworks;

namespace nugraph;

/// <summary>
/// Runs <c>dotnet</c> commands.
/// </summary>
internal static partial class Dotnet
{
    public static async Task<NuGetFramework?> GetLatestTargetFrameworkAsync(FileInfo source, CancellationToken cancellationToken)
    {
        void ConfigureArgs(ArgumentsBuilder args)
        {
            args.Add(source.FullName);
            args.Add($"--getItem:{nameof(Item.SupportedNETCoreAppTargetFramework)}");
        }

        var (_, items) = await RestoreAsync(ConfigureArgs, cancellationToken);

        var supportedTargetFrameworks = items.GetSupportedTargetFrameworks();
        return supportedTargetFrameworks.LastOrDefault();
    }

    public static async Task<ProjectInfo> RestoreAsync(FileSystemInfo? source, CancellationToken cancellationToken)
    {
        void ConfigureArgs(ArgumentsBuilder args)
        {
            if (source != null)
            {
                args.Add(source.FullName);
            }

            // !!! Requires a recent .NET SDK (see https://github.com/dotnet/msbuild/issues/3911)
            // arguments.Add("--target:ResolvePackageAssets"); // may enable if the project is an exe in order to get RuntimeCopyLocalItems + NativeCopyLocalItems
            args.Add($"--getProperty:{nameof(Property.ProjectAssetsFile)}");
            args.Add($"--getProperty:{nameof(Property.TargetFramework)}");
            args.Add($"--getProperty:{nameof(Property.TargetFrameworks)}");
            args.Add($"--getItem:{nameof(Item.RuntimeCopyLocalItems)}");
            args.Add($"--getItem:{nameof(Item.NativeCopyLocalItems)}");
        }

        var (properties, items) = await RestoreAsync(ConfigureArgs, cancellationToken);
        return new ProjectInfo(properties.GetProjectAssetsFile(), properties.GetTargetFrameworks(), items.GetNuGetPackageIds());
    }

    private static async Task<Result> RestoreAsync(Action<ArgumentsBuilder> configureArgs, CancellationToken cancellationToken)
    {
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        var jsonPipe = new JsonPipeTarget<Result>(SourceGenerationContext.Default.Result);
        var dotnet = Cli.Wrap("dotnet")
            .WithArguments(args =>
            {
                args.Add("restore");
                configureArgs(args);
            })
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
            if (message.Contains("MSB1001"))
            {
                throw new Exception("nugraph requires the .NET 8 SDK. Make sure that it's installed and that the global.json file (if any) is configured to use it.");
            }
            throw new Exception($"Running \"{dotnet}\" in \"{dotnet.WorkingDirPath}\" failed with exit code {commandResult.ExitCode}.{Environment.NewLine}{message}");
        }

        return jsonPipe.Result ?? throw new Exception($"Running \"{dotnet}\" in \"{dotnet.WorkingDirPath}\" returned a literal 'null' JSON payload");
    }

    public record ProjectInfo(FileInfo ProjectAssetsFile, IReadOnlyCollection<string> TargetFrameworks, IReadOnlyCollection<string> CopyLocalPackages);

    [JsonSerializable(typeof(Result))]
    private partial class SourceGenerationContext : JsonSerializerContext;

    private record Result(Property Properties, Item Items);

    private record Property(string? ProjectAssetsFile, string? TargetFramework, string? TargetFrameworks)
    {
        public IReadOnlyCollection<string> GetTargetFrameworks()
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

    [SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Must match the MSBuild item name found in the Microsoft.NET.SupportedTargetFrameworks.props file")]
    private record Item(CopyLocalItem[]? RuntimeCopyLocalItems, CopyLocalItem[]? NativeCopyLocalItems, SupportedTargetFramework[]? SupportedNETCoreAppTargetFramework)
    {
        public HashSet<string> GetNuGetPackageIds()
        {
            var runtimeCopyLocalItems = RuntimeCopyLocalItems ?? throw new Exception($"{nameof(RuntimeCopyLocalItems)} is missing");
            var nativeCopyLocalItems = NativeCopyLocalItems ?? throw new Exception($"{nameof(NativeCopyLocalItems)} is missing");
            return runtimeCopyLocalItems.Concat(nativeCopyLocalItems).Select(e => e.NuGetPackageId).OfType<string>().ToHashSet();
        }

        public List<NuGetFramework> GetSupportedTargetFrameworks()
        {
            var supportedTargetFrameworks = SupportedNETCoreAppTargetFramework ?? throw new Exception($"{nameof(SupportedNETCoreAppTargetFramework)} is missing");
            return supportedTargetFrameworks
                .Select(e => e.Identity == null ? null : NuGetFramework.Parse(e.Identity))
                .OfType<NuGetFramework>()
                .ToList();
        }
    }

    private record CopyLocalItem(string? NuGetPackageId);

    private record SupportedTargetFramework(string? Identity);
}