using System.Collections.Generic;
using System.Threading.Tasks;

namespace nugraph.Tests;

public abstract class Nugraph
{
    public abstract Task<(int ExitCode, IReadOnlyList<string> StdOut, IReadOnlyList<string> StdErr)> RunAsync(string[] arguments, string? workingDirectory = null);

    public abstract string Version { get; }
}