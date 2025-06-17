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
    [CommandArgument(0, "[SOURCE]")]
    [Description("The source of the graph. Can be either a directory containing a .NET project, a .NET project file (csproj) or the name of a NuGet package, " +
                 "optionally with a specific version, e.g. [b]Newtonsoft.Json/13.0.3[/].")]
    public string? SourceInput { get; init; }

    public FileOrPackage Source { get; private set; } = (FileSystemInfo?)null;

    [CommandOption("-o|--output <OUTPUT>")]
    [Description("The path to the dependency graph output file. If not specified, the dependency graph URL is written on the standard output and a live editor is opened in the default browser.")]
    public FileInfo? OutputFile { get; init; }

    [CommandOption("-f|--framework <FRAMEWORK>")]
    [Description("The target framework to consider when building the dependency graph.")]
    [TypeConverter(typeof(NuGetFrameworkConverter))]
    public NuGetFramework? Framework { get; init; }

    [CommandOption("-r|--runtime <RUNTIME_IDENTIFIER>")]
    [Description("The target runtime to consider when building the dependency graph.")]
    public string? RuntimeIdentifier { get; init; }

    [CommandOption("-e|--editor <FORMAT>")]
    [Description($"The live editor to use when the [b]--output[/] option is not specified.\n" +
                 $"Use [b]mmd[/] or [b]mermaid[/] for Mermaid Live Editor https://mermaid.live\n" +
                 $"Use [b]dot[/], [b]gv[/] or [b]graphviz[/] for Edotor https://edotor.net")]
    [DefaultValue("mermaid")]
    public string EditorInput { get; init; } = "";

    public LiveEditor.Service Editor { get; private set; }

    [CommandOption("-d|--direction <GRAPH_DIRECTION>")]
    [Description($"The direction of the dependency graph. Possible values are [b]{nameof(GraphDirection.LeftToRight)}[/] and [b]{nameof(GraphDirection.TopToBottom)}[/]")]
    [DefaultValue(GraphDirection.LeftToRight)]
    public GraphDirection GraphDirection { get; init; }

    [CommandOption("-t|--title <GRAPH_TITLE>")]
    [Description("The title of the dependency graph. Defaults to [b]Dependency graph of [i][[SOURCE]][/][/]")]
    public string? Title { get; set; }

    [CommandOption("-v|--include-version")]
    [Description("Include package versions in the dependency graph. E.g. [b]Serilog/3.1.1[/] instead of [b]Serilog[/]")]
    [DefaultValue(false)]
    public bool GraphIncludeVersions { get; init; }

    [CommandOption("-i|--ignore")]
    [Description("Packages to ignore in the dependency graph. May be used multiple times.")]
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

    [CommandOption("--nuget-root", IsHidden = true)]
    [Description("The NuGet root directory. Can be used to completely isolate nugraph from default NuGet operations.")]
    public string? NuGetRoot { get; init; }

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

        if (Source.TryPickT0(out var fileSystemInfo, out _))
        {
            var name = fileSystemInfo == null ? Path.GetFileNameWithoutExtension(Environment.CurrentDirectory) : Path.GetFileNameWithoutExtension(fileSystemInfo.Name);
            Title ??= $"Dependency graph of {name}";
        }

        if (EditorInput.StartsWith("mermaid", StringComparison.OrdinalIgnoreCase) || EditorInput.StartsWith("mmd", StringComparison.OrdinalIgnoreCase))
        {
            var editMode = EditorInput.EndsWith("-e", StringComparison.OrdinalIgnoreCase) || EditorInput.EndsWith("-edit", StringComparison.OrdinalIgnoreCase);
            Editor = editMode ? LiveEditor.Service.MermaidLiveEdit : LiveEditor.Service.MermaidLiveView;
        }
        else if (EditorInput.Equals("dot", StringComparison.OrdinalIgnoreCase) || EditorInput.Equals("gv", StringComparison.OrdinalIgnoreCase) || EditorInput.Equals("graphviz", StringComparison.OrdinalIgnoreCase))
        {
            Editor = LiveEditor.Service.Edotor;
        }
        else
        {
            return ValidationResult.Error($"{EditorInput} is not a valid live editor. Valid values are mmd, mermaid, dot, gv and graphviz.");
        }

        return base.Validate();
    }

    private FileOrPackage GetSource()
    {
        if (SourceInput == null)
            return (FileSystemInfo?)null;

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

    [SuppressMessage("Design", "CA1032:Implement standard exception constructors", Justification = "Not needed")]
    [SuppressMessage("Design", "CA1064:Exceptions should be public", Justification = "Not needed")]
    private sealed class InvalidNuGetVersionException(string packageName, string version) : Exception
    {
        public string PackageName { get; } = packageName;
        public string Version { get; } = version;
    }
}