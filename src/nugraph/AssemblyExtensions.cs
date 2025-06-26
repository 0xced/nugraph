using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using NuGet.Versioning;

namespace nugraph;

public static class AssemblyExtensions
{
    /// <summary>
    /// Returns the assembly version.
    /// <list type="bullet">
    ///   <item>Gets the <see cref="AssemblyInformationalVersionAttribute.InformationalVersion"/> from <see cref="AssemblyInformationalVersionAttribute"/>.</item>
    ///   <item>If found, tries to parse it as a semantic version and return the normalized string, i.e. without the <see cref="SemanticVersion.Metadata"/> part.</item>
    ///   <item>If the informational version is not found, returns the version from the <see cref="AssemblyName"/>.</item>
    ///   <item>If the assembly name version is not found, returns <c>N/A</c>.</item>
    /// </list>
    /// </summary>
    public static string GetVersion(this Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? assembly.GetName().Version?.ToString() ?? "N/A";
        return SemanticVersion.TryParse(version, out var semanticVersion) ? semanticVersion.ToNormalizedString() : version;
    }

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