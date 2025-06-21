using System;
using System.Threading.Tasks;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Xunit;

namespace nugraph.Tests;

public sealed class IntegrationTests(GlobalTool nugraph) : IClassFixture<GlobalTool>, IDisposable
{
    private readonly AssertionScope _scope = new();

    public void Dispose() => _scope.Dispose();

    [Fact]
    public async Task Version()
    {
        var (exitCode, stdOut, stdErr) = await nugraph.RunAsync("--version");

        exitCode.Should().Be(0);
        stdErr.Should().BeEmpty();
        stdOut.Should().ContainSingle().Which.Should().Be(nugraph.Version);
    }

    [Fact]
    public async Task Help()
    {
        var (exitCode, stdOut, stdErr) = await nugraph.RunAsync("--help");

        exitCode.Should().Be(0);
        stdErr.Should().BeEmpty();
        stdOut.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Package_Serilog()
    {
        var (exitCode, stdOut, stdErr) = await nugraph.RunAsync("Serilog", "--log", "Warning", "--no-browser");

        exitCode.Should().Be(0);
        stdErr.Should().BeEmpty();
        stdOut.Should().HaveCount(3);
        stdOut[0].Should().Be("Generating dependency graph for Serilog");
        stdOut[1].Should().StartWith("Generating dependency graph for Serilog ");
        stdOut[2].Should().StartWith("https://mermaid.live/view#pako:");
    }

    [Fact]
    public async Task Package_Serilog430()
    {
        var (exitCode, stdOut, stdErr) = await nugraph.RunAsync("Serilog/4.3.0", "--log", "Warning", "--no-browser");

        exitCode.Should().Be(0);
        stdErr.Should().BeEmpty();
        stdOut.Should().HaveCount(3);
        stdOut[0].Should().Be("Generating dependency graph for Serilog 4.3.0");
        stdOut[1].Should().StartWith("Generating dependency graph for Serilog 4.3.0 (");
        stdOut[2].Should().StartWith("https://mermaid.live/view#pako:");
    }
}
