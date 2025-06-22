using System.Runtime.CompilerServices;
using AwesomeAssertions;

namespace nugraph.Tests;

public sealed class ModuleInitializer
{
    [ModuleInitializer]
    public static void Initialize()
    {
        AssertionEngine.Configuration.Formatting.StringPrintLength = ushort.MaxValue;
    }
}