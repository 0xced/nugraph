using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Espresso3389.HttpStream;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace nugraph;

public class NuGetPackageResolver
{
    private readonly ISettings _settings;
    private readonly ILogger _logger;
    private readonly IList<PackageSource> _packageSources;
    private readonly SourceCacheContext _sourceCacheContext;

    public NuGetPackageResolver(ISettings settings, ILogger logger, IList<PackageSource> packageSources, SourceCacheContext sourceCacheContext)
    {
        _settings = settings;
        _logger = logger;
        _packageSources = packageSources;
        _sourceCacheContext = sourceCacheContext;
    }

    public async Task<(PackageIdentity Identity, IReadOnlyCollection<NuGetFramework> Frameworks)> ResolveAsync(PackageIdentity package, CancellationToken cancellationToken)
    {
        var packageSources = GetPackageSources(package);

        foreach (var sourceRepository in packageSources.Select(e => Repository.Factory.GetCoreV3(e)))
        {
            var packageInfo = await GetRemoteSourceDependencyInfoAsync(package, _sourceCacheContext, sourceRepository, cancellationToken);
            if (packageInfo != null)
            {
                // Don't use FindPackageByIdResource + GetDependencyInfoAsync because it downloads the full nupkg
                // Using HttpStream (which does HTTP range requests) is much more efficient
                var packageUri = new Uri(packageInfo.ContentUri);
                _logger.LogDebug($"Retrieving supported frameworks for {packageUri}");
                await using var packageStream = await HttpStream.CreateAsync(packageUri, cancellationToken);
                using var reader = new PackageArchiveReader(packageStream, leaveStreamOpen: true);
                var supportedFrameworks = (await reader.GetSupportedFrameworksAsync(cancellationToken)).Where(e => e.IsSpecificFramework).ToHashSet();
                _logger.LogDebug($"  => {(supportedFrameworks.Count == 0 ? "∅" : string.Join(", ", supportedFrameworks.Select(e => e.GetShortFolderName())))}");
                return (packageInfo.Identity, supportedFrameworks);
            }
        }

        if (packageSources.Count == 1)
        {
            throw new Exception($"Package {package} was not found in {packageSources[0]}");
        }

        throw new Exception($"Package {package} was not found. The following sources were searched {string.Join(", ", packageSources.Select(e => e.ToString()))}");
    }

    private IList<PackageSource> GetPackageSources(PackageIdentity package)
    {
        var packageSourceMapping = PackageSourceMapping.GetPackageSourceMapping(_settings);
        if (packageSourceMapping.IsEnabled)
        {
            var sourceNames = packageSourceMapping.GetConfiguredPackageSources(package.Id);
            return _packageSources.Where(e => sourceNames.Contains(e.Name)).ToList();
        }

        return _packageSources;
    }

    private async Task<RemoteSourceDependencyInfo?> GetRemoteSourceDependencyInfoAsync(PackageIdentity package, SourceCacheContext sourceCacheContext, SourceRepository sourceRepository, CancellationToken cancellationToken)
    {
        _logger.LogDebug($"Retrieving DependencyInfoResource for {sourceRepository}");
        var dependencyInfoResource = await sourceRepository.GetResourceAsync<DependencyInfoResource>(cancellationToken);
        _logger.LogDebug($"Resolving {package.Id}");
        var packageInfos = await dependencyInfoResource.ResolvePackages(package.Id, sourceCacheContext, _logger, cancellationToken);

        if (package.HasVersion)
        {
            var versionMatch = packageInfos.FirstOrDefault(e => e.Identity.Version == package.Version);
            _logger.LogDebug($"  => {package.Id}{(versionMatch != null ? $"/{versionMatch.Identity.Version} found" : $"/{package.Version} not found")}");
            return versionMatch;
        }

        RemoteSourceDependencyInfo? release = null;
        RemoteSourceDependencyInfo? preRelease = null;
        foreach (var packageInfo in packageInfos.Where(p => p.Listed))
        {
            switch (packageInfo.Identity.Version.IsPrerelease)
            {
                case true when preRelease == null || packageInfo.Identity.Version > preRelease.Identity.Version:
                    preRelease = packageInfo;
                    break;
                case false when release == null || packageInfo.Identity.Version > release.Identity.Version:
                    release = packageInfo;
                    break;
            }
        }

        var latestVersion = release ?? preRelease;
        _logger.LogDebug($"  => {package.Id}{(latestVersion != null ? $"/{latestVersion.Identity.Version}" : " not found")}");
        return latestVersion;
    }
}