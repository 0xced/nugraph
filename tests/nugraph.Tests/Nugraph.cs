using System.Threading.Tasks;
using NuGet.Common;

namespace nugraph.Tests;

public abstract class Nugraph
{
    public abstract Task<(int ExitCode, string StdOut, string StdErr)> RunAsync(string[] arguments, string? workingDirectory = null, LogLevel logLevel = LogLevel.Warning, bool noBrowser = true);

    public abstract string Version { get; }
}