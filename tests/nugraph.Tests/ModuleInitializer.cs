using System;
using System.Runtime.CompilerServices;
using AwesomeAssertions;

namespace nugraph.Tests;

public sealed class ModuleInitializer
{
    [ModuleInitializer]
    public static void Initialize()
    {
        Environment.SetEnvironmentVariable("DOTNET_SYSTEM_CONSOLE_ALLOW_ANSI_COLOR_REDIRECTION", "false");
        AssertionEngine.Configuration.Formatting.StringPrintLength = ushort.MaxValue;
    }
}