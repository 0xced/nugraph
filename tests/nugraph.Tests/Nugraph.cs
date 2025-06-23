using System.Threading.Tasks;

namespace nugraph.Tests;

public abstract class Nugraph
{
    public abstract Task<(int ExitCode, string StdOut, string StdErr)> RunAsync(string[] arguments, string? workingDirectory = null);

    public abstract string Version { get; }
}