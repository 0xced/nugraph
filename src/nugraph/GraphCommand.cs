using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Chisel;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;
using OneOf;
using Spectre.Console;
using Spectre.Console.Cli;

namespace nugraph;

[GenerateOneOf]
internal sealed partial class FileOrPackage : OneOfBase<FileSystemInfo?, PackageIdentity>
{
    public override string ToString() => Match(file => file?.FullName ?? Environment.CurrentDirectory, package => package.ToString());
}

[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global", Justification = "Instantiated by Spectre.Console.Cli through reflection")]
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by Spectre.Console.Cli through reflection")]
[Description("Generates dependency graphs for .NET projects and NuGet packages.")]
internal sealed class GraphCommand(IAnsiConsole console, CancellationToken cancellationToken) : CancelableCommand<GraphCommandSettings>(cancellationToken)
{
    protected override async Task<int> ExecuteAsync(CommandContext commandContext, GraphCommandSettings settings, CancellationToken cancellationToken)
    {
        var source = settings.Source;
        var graphUrl = await console.Status().StartAsync($"Generating dependency graph for {source}", async context =>
        {
            var graph = await source.Match(
                file => ComputeDependencyGraphAsync(file, settings, cancellationToken),
                package => ComputeDependencyGraphAsync(package, settings, new SpectreLogger(console, settings.LogLevel), context, cancellationToken)
            );
            return await WriteGraphAsync(graph, settings);
        });

        if (graphUrl != null)
        {
            var url = graphUrl.ToString();
#pragma warning disable Spectre1000
            // Using "console.WriteLine(url)" (lowercase c) would insert newlines at the physical console length, making the written URL neither copyable nor clickable
            // At that point the status has terminated so it's fine not using the IAnsiConsole methods
            Console.WriteLine(url);
#pragma warning restore Spectre1000
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        else if (settings.OutputFile != null)
        {
            console.MarkupLineInterpolated($"The {source} dependency graph has been written to [lime]{new Uri(settings.OutputFile.FullName)}[/]");
        }

        return 0;
    }

    private static async Task<DependencyGraph> ComputeDependencyGraphAsync(FileSystemInfo? source, GraphCommandSettings settings, CancellationToken cancellationToken)
    {
        var projectInfo = await Dotnet.RestoreAsync(source, cancellationToken);
        var targetFramework = settings.Framework ?? projectInfo.TargetFrameworks.Select(NuGetFramework.Parse).First();
        var lockFile = new LockFileFormat().Read(projectInfo.ProjectAssetsFile.FullName);
        Predicate<Package> filter = projectInfo.CopyLocalPackages.Count > 0 ? package => projectInfo.CopyLocalPackages.Contains(package.Name) : _ => true;
        var (packages, roots) = lockFile.ReadPackages(targetFramework.GetShortFolderName(), settings.RuntimeIdentifier, filter);
        return new DependencyGraph(packages, roots, ignores: settings.GraphIgnore);
    }

    private static async Task<DependencyGraph> ComputeDependencyGraphAsync(PackageIdentity package, GraphCommandSettings settings, ILogger logger, StatusContext context, CancellationToken cancellationToken)
    {
        using var project = await TemporaryProject.CreateAsync(package, settings.Framework, logger, cancellationToken);
        settings.Title ??= $"Dependency graph of {project.Package.Id} {project.Package.Version} ({project.TargetFramework.GetShortFolderName()})";
        context.Status = $"Generating dependency graph for {project.Package.Id} {project.Package.Version} ({project.TargetFramework.GetShortFolderName()})";
        return await ComputeDependencyGraphAsync(project.File, settings, cancellationToken);
    }

    private static async Task<Uri?> WriteGraphAsync(DependencyGraph graph, GraphCommandSettings settings)
    {
        await using var fileStream = settings.OutputFile?.OpenWrite();
        await using var memoryStream = fileStream == null ? new MemoryStream(capacity: 2048) : null;
        var stream = (fileStream ?? memoryStream as Stream)!;
        await using (var streamWriter = new StreamWriter(stream, leaveOpen: true))
        {
            bool isMermaid;
            if (fileStream == null)
                isMermaid = settings.Editor is LiveEditor.Service.MermaidLiveView or LiveEditor.Service.MermaidLiveEdit;
            else
                isMermaid = Path.GetExtension(fileStream.Name) is ".mmd" or ".mermaid";

            var graphWriter = isMermaid ? GraphWriter.Mermaid(streamWriter) : GraphWriter.Graphviz(streamWriter);
            var graphOptions = new GraphOptions
            {
                Direction = settings.GraphDirection,
                Title = settings.Title,
                IncludeLinks = !settings.NoLinks,
                IncludeVersions = settings.GraphIncludeVersions,
                WriteIgnoredPackages = settings.GraphWriteIgnoredPackages,
            };
            graphWriter.Write(graph, graphOptions);
        }

        return memoryStream == null ? null : LiveEditor.GetUri(memoryStream.AsSpan(), settings.Editor);
    }
}