using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using static NuGet.Frameworks.FrameworkConstants.CommonFrameworks;

namespace nugraph;

public sealed class TemporaryProject : IDisposable
{
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private readonly DirectoryInfo _directory;

    public TemporaryProject() : this(package: null, targetFramework: null)
    {
    }

    private TemporaryProject(PackageIdentity? package, NuGetFramework? targetFramework)
    {
        _directory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), "nugraph", Path.GetRandomFileName().Replace(".", "", StringComparison.OrdinalIgnoreCase)));
        _directory.Create();

        var project = new XElement("Project", new XAttribute("Sdk", "Microsoft.NET.Sdk"));

        if (targetFramework != null)
        {
            project.Add(new XElement("PropertyGroup",
                new XElement("TargetFramework", targetFramework.GetShortFolderName())));
        }

        if (package != null)
        {
            project.Add(new XElement("ItemGroup",
                new XElement("PackageReference", new XAttribute("Include", package.Id), new XAttribute("Version", package.Version?.ToString() ?? "*"))));
        }

        File = new FileInfo(Path.Combine(_directory.FullName, "project.csproj"));
        Package = package ?? new PackageIdentity("", new NuGetVersion(0, 0, 0));
        TargetFramework = targetFramework ?? NuGetFramework.UnsupportedFramework;

        var settings = new XmlWriterSettings { Indent = true, OmitXmlDeclaration = true, Encoding = Utf8NoBom };
        using var xmlWriter = XmlWriter.Create(File.FullName, settings);
        project.Save(xmlWriter);
    }

    public static async Task<TemporaryProject> CreateAsync(PackageIdentity package, NuGetFramework? targetFramework, DirectoryInfo? sdk, ISettings nugetSettings, ILogger logger, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(logger, nameof(logger));

        var (identity, resolvedTargetFramework) = await ResolveAsync(package, targetFramework, sdk, nugetSettings, logger, cancellationToken);
        return new TemporaryProject(identity, resolvedTargetFramework);
    }

    private static async Task<(PackageIdentity Identity, NuGetFramework Framework)> ResolveAsync(PackageIdentity package, NuGetFramework? framework, DirectoryInfo? sdk, ISettings nugetSettings, ILogger logger, CancellationToken cancellationToken)
    {
        using var sourceCacheContext = new SourceCacheContext();
        var packageSources = GetPackageSources(nugetSettings, logger);
        var packageIdentityResolver = new NuGetPackageResolver(nugetSettings, logger, packageSources, sourceCacheContext);

        var (identity, targetFrameworks) = await packageIdentityResolver.ResolveAsync(package, cancellationToken);

        if (framework != null)
        {
            if (targetFrameworks.Count > 0 && targetFrameworks.All(f => !DefaultCompatibilityProvider.Instance.IsCompatible(framework, f)))
            {
                var tfms = string.Join(", ", targetFrameworks.Select(e => e.GetShortFolderName()));
                logger.LogWarning($"The specified framework ({framework.GetShortFolderName()}) is not compatible with the supported frameworks of {identity} ({tfms})");
            }

            return (identity, framework);
        }

        var sdkPath = DotnetSdk.Register(sdk);
        logger.LogDebug($"Using .NET SDK at {sdkPath}");
        var supportedTargetFrameworks = DotnetSdk.GetSupportedTargetFrameworks();

        var supportedTargetFramework = targetFrameworks.Intersect(supportedTargetFrameworks).Order(NuGetFrameworkVersionComparer.Instance).FirstOrDefault();
        if (supportedTargetFramework != null)
        {
            return (identity, supportedTargetFramework);
        }

        var targetFramework = targetFrameworks.Order(NuGetFrameworkVersionComparer.Instance).FirstOrDefault();
        if (targetFramework != null)
        {
            return (identity, targetFramework);
        }

        return (identity, NetStandard10);
    }

    private static List<PackageSource> GetPackageSources(ISettings settings, ILogger logger)
    {
        var packageSourceProvider = new PackageSourceProvider(settings);
        var packageSources = packageSourceProvider.LoadPackageSources().Where(e => e.IsEnabled).Distinct().ToList();

        if (packageSources.Count == 0)
        {
            var officialPackageSource = new PackageSource(NuGetConstants.V3FeedUrl, NuGetConstants.NuGetHostName);
            packageSources.Add(officialPackageSource);
            var configFilePaths = settings.GetConfigFilePaths().Distinct();
            logger.LogWarning($"No NuGet sources could be found in {string.Join(", ", configFilePaths)}. Using {officialPackageSource}");
        }

        return packageSources;
    }

    public void Dispose()
    {
        _directory.Delete(recursive: true);
    }

    public FileInfo File { get; }

    public PackageIdentity Package { get; }

    public NuGetFramework TargetFramework { get; }
}