using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Locator;
using NuGet.Frameworks;

namespace nugraph;

public static class DotnetSdk
{
    // Required for Microsoft.Build.* classes to work properly.
    public static string? Register(DirectoryInfo? sdk)
    {
        if (MSBuildLocator.CanRegister)
        {
            if (sdk != null)
            {
                MSBuildLocator.RegisterMSBuildPath(sdk.FullName);
                return sdk.FullName;
            }

            var instance = MSBuildLocator.RegisterDefaults();
            return instance.MSBuildPath;
        }

        return null;
    }

    public static async Task<IReadOnlyCollection<NuGetFramework>> GetSupportedTargetFrameworksAsync(CancellationToken cancellationToken)
    {
        using var projectCollection = new ProjectCollection();
        var sdkPath = projectCollection.Toolsets.First(e => e.ToolsVersion == projectCollection.DefaultToolsVersion).ToolsPath;

        var cachedFrameworks = await SupportedFrameworks.Cache.FindAsync(sdkPath, cancellationToken);
        if (cachedFrameworks != null)
        {
            return cachedFrameworks;
        }

        using var xmlReader = new XElement("Project", new XAttribute("Sdk", "Microsoft.NET.Sdk")).CreateReader();
        var project = new Project(xmlReader, globalProperties:null, toolsVersion: null, projectCollection);

        var supportedTargetFrameworks = project.Items
            .Where(e => e.ItemType == "SupportedTargetFramework")
            .Select(e => NuGetFramework.Parse(e.EvaluatedInclude))
            .ToHashSet();

        return await SupportedFrameworks.Cache.SetAsync(sdkPath, supportedTargetFrameworks, cancellationToken);
    }
}