using System.Collections.Generic;
using NuGet.Frameworks;

namespace nugraph;

internal sealed class NuGetFrameworkVersionComparer : IComparer<NuGetFramework>
{
    public static NuGetFrameworkVersionComparer Instance { get; } = new();

    public int Compare(NuGetFramework? x, NuGetFramework? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (x is null)
        {
            return -1;
        }

        if (y is null)
        {
            return 1;
        }

        return y.Version.CompareTo(x.Version);
    }
}