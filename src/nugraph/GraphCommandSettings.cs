using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Chisel;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using Spectre.Console;
using Spectre.Console.Cli;

namespace nugraph;

[SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Global", Justification = "Required for Spectre.Console.Cli binding")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global", Justification = "Required for Spectre.Console.Cli binding")]
[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global", Justification = "Instantiated by Spectre.Console.Cli through reflection")]
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by Spectre.Console.Cli through reflection")]
internal sealed class GraphCommandSettings : CommandSettings
{
    public const string DefaultTitle = "Dependency graph of [SOURCE]";

    [CommandArgument(0, "[SOURCE]")]
    [Description("The source of the graph. Can be either a directory containing a .NET project, a .NET project file (csproj) or the name of a NuGet package, " +
                 "optionally with a specific version, e.g. [b]Newtonsoft.Json/13.0.3[/].")]
    public string? SourceInput { get; init; }

    public FileOrPackage? Source { get; private set; }

    [CommandOption("-o|--output <OUTPUT>")]
    [Description("The path to the dependency graph output file. If not specified, the dependency graph URL is written on the standard output and an online service is opened in the default browser.")]
    public FileInfo? OutputFile { get; init; }

    [CommandOption("-f|--framework <FRAMEWORK>")]
    [Description("The target framework to consider when building the dependency graph.")]
    [TypeConverter(typeof(NuGetFrameworkConverter))]
    public NuGetFramework? Framework { get; init; }

    [CommandOption("-r|--runtime <RUNTIME_IDENTIFIER>")]
    [Description("The target runtime to consider when building the dependency graph.")]
    public string? RuntimeIdentifier { get; init; }

    [CommandOption("-m|--format <FORMAT>")]
    [Description($"The format to use when the [b]--output[/] option is not specified.\n" +
                 $"Use [b]mmd[/] or [b]mermaid[/] for Mermaid Live Editor https://mermaid.live\n" +
                 $"Use [b]dot[/], [b]gv[/] or [b]graphviz[/] for Edotor https://edotor.net")]
    [DefaultValue("mermaid")]
    public string Format { get; init; } = "";

    public OnlineService Service { get; private set; }

    [CommandOption("-d|--direction <GRAPH_DIRECTION>")]
    [Description($"The direction of the dependency graph. Possible values are [b]{nameof(GraphDirection.LeftToRight)}[/] and [b]{nameof(GraphDirection.TopToBottom)}[/]")]
    [DefaultValue(GraphDirection.LeftToRight)]
    public GraphDirection GraphDirection { get; init; }

    [CommandOption("-t|--title <GRAPH_TITLE>")]
    [Description("The title of the dependency graph.")]
    [DefaultValue(DefaultTitle)]
    public string Title { get; set; } = "";

    [CommandOption("-s|--include-version")]
    [Description("Include package versions in the dependency graph. E.g. [b]Serilog/4.3.0[/] instead of [b]Serilog[/]")]
    [DefaultValue(false)]
    public bool GraphIncludeVersions { get; init; }

    [CommandOption("-i|--ignore <PACKAGE>")]
    [Description("Packages to ignore in the dependency graph. Supports * wildcards. May be used multiple times.")]
    public string[] GraphIgnore { get; init; } = [];

    [CommandOption("--no-links")]
    [Description("Remove clickable links from the the dependency graph. Can be useful to reduce the size of the graph if you get \"Maximum text size in diagram exceeded\" in Mermaid Live Editor.")]
    [DefaultValue(false)]
    public bool NoLinks { get; set; }

    [CommandOption("--no-browser")]
    [Description("Do not open the default browser, only print the graph URL on the console when the [b]--output[/] option is not specified.")]
    [DefaultValue(false)]
    public bool NoBrowser { get; set; }

    [CommandOption("-l|--log <LEVEL>")]
    [Description($"The NuGet operations log level. Possible values are [b]{nameof(LogLevel.Debug)}[/], [b]{nameof(LogLevel.Verbose)}[/], [b]{nameof(LogLevel.Information)}[/], [b]{nameof(LogLevel.Minimal)}[/], [b]{nameof(LogLevel.Warning)}[/] and [b]{nameof(LogLevel.Error)}[/]")]
#if DEBUG
    [DefaultValue(LogLevel.Debug)]
#else
    [DefaultValue(LogLevel.Warning)]
#endif
    public LogLevel LogLevel { get; init; }

    [CommandOption("--nuget-root <PATH>", IsHidden = true)]
    [Description("The NuGet root directory. Can be used to completely isolate nugraph from default NuGet operations.")]
    public string? NuGetRoot { get; init; }

    [CommandOption("--sdk <PATH>", IsHidden = true)]
    [Description("Path to the .NET SDK directory. E.g. [b]/usr/local/share/dotnet/sdk/8.0.410[/]")]
    public DirectoryInfo? Sdk { get; init; }

    [CommandOption("--include-ignored-packages", IsHidden = true)]
    [Description("Include ignored packages in the dependency graph. Used for debugging.")]
    [DefaultValue(false)]
    public bool GraphWriteIgnoredPackages { get; init; }

    public override ValidationResult Validate()
    {
        try
        {
            Source = GetSource();
        }
        catch (InvalidNuGetVersionException exception)
        {
            return ValidationResult.Error($"Version {exception.Version} for package {exception.PackageName} is not a valid NuGet version.");
        }

        Service = GetOnlineService(Format);
        if (!Enum.IsDefined(typeof(OnlineService), Service))
        {
            return ValidationResult.Error($"{Format} is not a supported format. Valid values are mmd, mermaid, dot, gv and graphviz.");
        }

        if (Sdk is { Exists: false })
        {
            return ValidationResult.Error($"The SDK directory ({Sdk}) must exist.");
        }

        return base.Validate();
    }

    private static OnlineService GetOnlineService(string fmt)
    {
        if (fmt.StartsWith("mermaid", StringComparison.OrdinalIgnoreCase) || fmt.StartsWith("mmd", StringComparison.OrdinalIgnoreCase))
        {
            if (fmt.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
            {
                return fmt.Contains("kroki", StringComparison.OrdinalIgnoreCase) ? OnlineService.MermaidKrokiSvg : OnlineService.MermaidInkSvg;
            }

            if (fmt.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                return fmt.Contains("kroki", StringComparison.OrdinalIgnoreCase) ? OnlineService.MermaidKrokiPng : OnlineService.MermaidInkPng;
            }

            if (fmt.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || fmt.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
            {
                return OnlineService.MermaidInkJpg;
            }

            if (fmt.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
            {
                return OnlineService.MermaidInkWebp;
            }

            if (fmt.EndsWith("-edit", StringComparison.OrdinalIgnoreCase))
            {
                return OnlineService.MermaidLiveEdit;
            }

            return OnlineService.MermaidLiveView;
        }

        if (fmt.StartsWith("dot", StringComparison.OrdinalIgnoreCase) || fmt.StartsWith("gv", StringComparison.OrdinalIgnoreCase) || fmt.StartsWith("graphviz", StringComparison.OrdinalIgnoreCase))
        {
            if (fmt.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                return OnlineService.GraphvizKrokiPng;
            }

            if (fmt.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
            {
                return OnlineService.GraphvizKrokiSvg;
            }

            if (fmt.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || fmt.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
            {
                return OnlineService.GraphvizKrokiJpg;
            }

            if (fmt.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                return OnlineService.GraphvizKrokiPdf;
            }

            return OnlineService.GraphvizEdotor;
        }

        return (OnlineService)(-1);
    }

    private FileOrPackage? GetSource()
    {
        if (SourceInput == null)
            return null;

        var file = new FileInfo(SourceInput);
        if (file.Exists)
        {
            return file;
        }

        var directory = new DirectoryInfo(SourceInput);
        if (directory.Exists)
        {
            return directory;
        }

        return GetPackageIdentity(SourceInput);
    }

    private static PackageIdentity GetPackageIdentity(string packageId)
    {
        var parts = packageId.Split('/');
        if (parts.Length == 2)
        {
            if (NuGetVersion.TryParse(parts[1], out var version))
            {
                return new PackageIdentity(parts[0], version);
            }

            throw new InvalidNuGetVersionException(packageName: parts[0], version: parts[1]);
        }

        return new PackageIdentity(packageId, version: null);
    }

    private sealed class InvalidNuGetVersionException(string packageName, string version) : Exception
    {
        public string PackageName { get; } = packageName;
        public string Version { get; } = version;
    }
}