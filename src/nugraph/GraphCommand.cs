using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Chisel;
using CliWrap;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;
using OneOf;
using Spectre.Console;
using Spectre.Console.Cli;

namespace nugraph;

[GenerateOneOf]
internal sealed partial class FileOrPackage : OneOfBase<FileSystemInfo, PackageIdentity>
{
    public override string ToString() => Match(file => file.Name, package => package.HasVersion ? $"{package.Id} {package.Version}" : package.Id);
}

[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global", Justification = "Instantiated by Spectre.Console.Cli through reflection")]
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by Spectre.Console.Cli through reflection")]
[Description("Generates dependency graphs for .NET projects and NuGet packages.")]
internal sealed class GraphCommand(ProgramEnvironment environment) : AsyncCommand<GraphCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext commandContext, GraphCommandSettings settings, CancellationToken cancellationToken)
    {
        var stdOut = environment.StdOut;
        var console = environment.ConsoleErr;

        if (settings.Diagnose)
        {
            return await DiagnoseAsync(stdOut, settings.Sdk, cancellationToken);
        }

        var source = settings.Source ?? environment.CurrentWorkingDirectory;
        var graphUrl = await console.Status().StartAsync($"Generating dependency graph for {source}".EscapeMarkup(), async context =>
        {
            var logger = new SpectreLogger(console, settings.LogLevel);
            var graph = await source.Match(
                file => ComputeDependencyGraphAsync(file, settings, logger, cancellationToken),
                package => ComputeDependencyGraphAsync(package, settings, Settings.LoadDefaultSettings(settings.NuGetRoot), logger, context, cancellationToken)
            );
            return await WriteGraphAsync(graph, settings);
        });

        if (graphUrl != null)
        {
            var url = graphUrl.ToString();
            if (settings.UrlAction.HasFlag(UrlAction.print))
            {
                // Using "console.WriteLine(url)" (lowercase c) would insert newlines at the physical console length, making the written URL neither copyable nor clickable
                // At that point the status has terminated so it's fine not using the IAnsiConsole methods
                await stdOut.WriteLineAsync(url);
            }
            if (settings.UrlAction.HasFlag(UrlAction.open))
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                console.WriteLine($"The {source} dependency graph has been opened in the default browser");
            }
        }
        else if (settings.OutputFile != null)
        {
            console.MarkupLineInterpolated($"The {source} dependency graph has been written to [lime]{new Uri(settings.OutputFile.FullName)}[/]");
        }

        return 0;
    }

    private static async Task<int> DiagnoseAsync(TextWriter stdOut, DirectoryInfo? sdk, CancellationToken cancellationToken)
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var userProfileReplacement = OperatingSystem.IsWindows() ? "%UserProfile%" : "~";

        await stdOut.WriteLineAsync("nugraph:");
        await stdOut.WriteLineAsync($" Version:  {typeof(Program).Assembly.GetVersion()}");
        await stdOut.WriteLineAsync($" Runtime:  {Environment.Version}");
        await stdOut.WriteLineAsync($" SDK:      {DotnetSdk.Register(sdk)?.Replace(userProfile, userProfileReplacement)}");
        await stdOut.WriteLineAsync();

        await stdOut.WriteLineAsync("attributes:");
        foreach (var attribute in typeof(Program).Assembly.GetCustomAttributesData().OrderBy(a => a.AttributeType.Name))
        {
            await stdOut.WriteLineAsync($" {attribute}");
        }
        await stdOut.WriteLineAsync();

        await stdOut.WriteLineAsync("assemblies:");
        foreach (var assembly in typeof(Program).Assembly.LoadReferencedAssemblies().OrderBy(a => a.GetName().Name))
        {
            await stdOut.WriteLineAsync($" {assembly}: {assembly.Location.Replace(userProfile, userProfileReplacement)}");
        }
        await stdOut.WriteLineAsync();

        var dotnetInfo = Cli.Wrap("dotnet").WithArguments("--info").WithStandardOutputPipe(PipeTarget.ToDelegate(line => stdOut.WriteLine(line.Replace(userProfile, userProfileReplacement))));
        var result = await dotnetInfo.ExecuteAsync(cancellationToken);
        return result.ExitCode;
    }

    private static async Task<DependencyGraph> ComputeDependencyGraphAsync(FileSystemInfo source, GraphCommandSettings settings, ILogger logger, CancellationToken cancellationToken)
    {
        var name = Path.GetFileNameWithoutExtension(source.Name);
        if (settings.Title == GraphCommandSettings.DefaultTitle)
        {
            settings.Title = $"Dependency graph of {name}";
        }
        var projectInfo = await DotnetCli.RestoreAsync(source, cancellationToken);
        var targetFramework = settings.Framework ?? projectInfo.TargetFrameworks.Select(NuGetFramework.Parse).First();
        var lockFile = new LockFileFormat().Read(projectInfo.ProjectAssetsFile.FullName);
        Predicate<Package> filter = projectInfo.CopyLocalPackages.Count > 0 ? package => projectInfo.CopyLocalPackages.Contains(package.Name) : _ => true;
        var (packages, roots) = lockFile.ReadPackages(targetFramework.GetShortFolderName(), settings.RuntimeIdentifier, filter);
        var dependencyGraph = new DependencyGraph(packages, roots, ignores: settings.GraphIgnore);
        if (!settings.NoLinks)
        {
            await dependencyGraph.AddLinksAsync(logger, cancellationToken);
        }
        return dependencyGraph;
    }

    private static async Task<DependencyGraph> ComputeDependencyGraphAsync(PackageIdentity package, GraphCommandSettings settings, ISettings nugetSettings, ILogger logger, StatusContext context, CancellationToken cancellationToken)
    {
        using var project = await TemporaryProject.CreateAsync(package, settings.Framework, settings.Sdk, nugetSettings, logger, cancellationToken);
        if (settings.Title == GraphCommandSettings.DefaultTitle)
        {
            settings.Title = $"Dependency graph of {project.Package.Id} {project.Package.Version} ({project.TargetFramework.GetShortFolderName()})";
        }
        context.Status = $"Generating dependency graph for {project.Package.Id} {project.Package.Version} ({project.TargetFramework.GetShortFolderName()})".EscapeMarkup();
        return await ComputeDependencyGraphAsync(project.File, settings, logger, cancellationToken);
    }

    private static async Task<Uri?> WriteGraphAsync(DependencyGraph graph, GraphCommandSettings settings)
    {
        await using var fileStream = settings.OutputFile?.OpenWrite();
        fileStream?.SetLength(0);
        await using var memoryStream = fileStream == null ? new MemoryStream(capacity: 2048) : null;
        var stream = (fileStream ?? memoryStream as Stream)!;
        await using (var streamWriter = new StreamWriter(stream, leaveOpen: true))
        {
            var isMermaid = fileStream != null ? Path.GetExtension(fileStream.Name) is ".mmd" or ".mermaid" : settings.Service.ToString().StartsWith("Mermaid");
            var graphWriter = isMermaid ? GraphWriter.Mermaid(streamWriter) : GraphWriter.Graphviz(streamWriter);
            var graphOptions = new GraphOptions
            {
                Direction = settings.GraphDirection,
                Title = settings.Title,
                IncludeVersions = settings.GraphIncludeVersions,
                WriteIgnoredPackages = settings.GraphWriteIgnoredPackages,
            };
            graphWriter.Write(graph, graphOptions);
        }

        return memoryStream == null ? null : GraphService.GetUri(memoryStream.AsSpan(), settings.Service);
    }
}