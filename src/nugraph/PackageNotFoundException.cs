using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Configuration;
using NuGet.Packaging.Core;

namespace nugraph;

public class PackageNotFoundException(PackageIdentity package, IList<PackageSource> sources) : Exception(GetMessage(package, sources))
{
    private static string GetMessage(PackageIdentity package, IList<PackageSource> sources)
    {
        return sources.Count switch
        {
            1 => $"Package {package} was not found in {sources[0]}",
            _ => $"Package {package} was not found. The following sources were searched {string.Join(", ", sources.Select(e => e.ToString()))}",
        };
    }
}