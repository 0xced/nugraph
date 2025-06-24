using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;

namespace nugraph;

internal static class AssemblyExtensions
{
    /// <summary>
    /// Recursively loads all referenced assemblies.
    /// </summary>
    public static IEnumerable<Assembly> LoadReferencedAssemblies(this Assembly rootAssembly)
    {
        ArgumentNullException.ThrowIfNull(rootAssembly);

        var assemblies = new HashSet<Assembly> { rootAssembly };
        LoadReferencedAssemblies(rootAssembly, assemblies);
        return assemblies;
    }

    private static void LoadReferencedAssemblies(Assembly rootAssembly, HashSet<Assembly> assemblies)
    {
        foreach (var assemblyName in rootAssembly.GetReferencedAssemblies())
        {
            if (TryLoad(assemblyName, out var assembly) && assemblies.Add(assembly))
            {
                LoadReferencedAssemblies(assembly, assemblies);
            }
        }
    }

    private static bool TryLoad(AssemblyName assemblyName, [NotNullWhen(true)] out Assembly? assembly)
    {
        try
        {
            assembly = Assembly.Load(assemblyName);
            return true;
        }
        catch (Exception exception) when (exception is FileLoadException or FileNotFoundException)
        {
            assembly = null;
            return false;
        }
    }
}