using System.Threading.Tasks;
using NuGet.Common;

namespace nugraph.Tests;

public abstract class Nugraph
{
    public abstract Task<NugraphResult> RunAsync(string[] arguments, string? workingDirectory = null, LogLevel logLevel = LogLevel.Warning, UrlAction action = UrlAction.print);

    public abstract string Version { get; }
}

public record struct NugraphResult(int ExitCode, string StdOut, string StdErr);