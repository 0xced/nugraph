using System;
using System.Collections.Concurrent;
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

namespace nugraph;

internal class TemporaryProject : IDisposable
{
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private readonly DirectoryInfo _directory;

    private TemporaryProject(PackageIdentity? package, NuGetFramework targetFramework)
    {
        _directory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), "nugraph", Path.GetRandomFileName().Replace(".", "")));
        _directory.Create();

        var project = new XElement("Project", new XAttribute("Sdk", "Microsoft.NET.Sdk"),
            new XElement("PropertyGroup",
                new XElement("TargetFramework", targetFramework.GetShortFolderName())));

        if (package != null)
        {
            project.Add(new XElement("ItemGroup",
                new XElement("PackageReference", new XAttribute("Include", package.Id), new XAttribute("Version", package.Version?.ToString() ?? "*" ))));
        }

        File = new FileInfo(Path.Combine(_directory.FullName, "project.csproj"));

        var settings = new XmlWriterSettings { Indent = true, OmitXmlDeclaration = true, Encoding = Utf8NoBom };
        using var xmlWriter = XmlWriter.Create(File.FullName, settings);
        project.Save(xmlWriter);
    }

    public static async Task<TemporaryProject> CreateAsync(PackageIdentity package, NuGetFramework? targetFramework, ILogger logger, CancellationToken cancellationToken)
    {
        var (identity, appropriateTargetFramework) = await ResolveAsync(package, logger, cancellationToken);
        return new TemporaryProject(identity, targetFramework ?? appropriateTargetFramework);
    }

    private static async Task<(PackageIdentity Identity, NuGetFramework Framework)> ResolveAsync(PackageIdentity package, ILogger logger, CancellationToken cancellationToken)
    {
       var nugetSettings = Settings.LoadDefaultSettings(null);
       using var sourceCacheContext = new SourceCacheContext();
       var packageSources = GetPackageSources(nugetSettings, logger);
       var packageIdentityResolver = new NuGetPackageResolver(nugetSettings, logger, packageSources, sourceCacheContext);

       var (identity, targetFrameworks) = await packageIdentityResolver.ResolveAsync(package, cancellationToken);

       var compatibleTfm = targetFrameworks.Order(NuGetFrameworkVersionComparer.Instance).FirstOrDefault();
       if (compatibleTfm != null)
       {
           return (identity, compatibleTfm);
       }

       using var emptyProject = new TemporaryProject(package: null, targetFramework: FrameworkConstants.CommonFrameworks.NetStandard20);
       var latestTfm = await Dotnet.GetLatestTargetFrameworkAsync(emptyProject.File, cancellationToken);
       return (identity, latestTfm ?? FrameworkConstants.CommonFrameworks.NetStandard10);
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
}