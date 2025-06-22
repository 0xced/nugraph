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
    public async Task Package_Serilog_430_net60()
    {
        var (exitCode, stdOut, stdErr) = await nugraph.RunAsync("Serilog/4.3.0", "--framework", "net6.0", "--log", "Warning", "--no-browser");

        exitCode.Should().Be(0);
        stdErr.Should().BeEmpty();
        stdOut.Should().HaveCount(3);
        stdOut[0].Should().Be("Generating dependency graph for Serilog 4.3.0");
        stdOut[1].Should().Be("Generating dependency graph for Serilog 4.3.0 (net6.0)");
        stdOut[2].Should().Be("https://mermaid.live/view#pako:eNpcUcFuhCAU_BXiZpM2UaG7m03KeZNeempvjRcWnkpEsPiMuzH-e1Vc05YLw8C8efMYIukURDxKkiSzqNEAJxdowCqw8k4KL5qSuJx8gtfGFeSUHlNGnizgOWXPmV10md3vyRtY8AJBkeudlIhNyyktNJbdNZWupuwmQVHbLSVnTaj9_jFjaUTbXiAn3jkkLXpXQdJrhSU_Nbdf9wpy0RkkuTaGi-9O1MJrC3GQ8B1jr-z8EktnnOe747Jmg7X_YVjBOG62W7bZ-z-3-s20ltVGZx1jh8MjZd_36RQMMHW-oI2QlSigpetjuswsKFbhn2kGKrNRHNXga6HV9B9DYLGEGgLkYXs0tBzGSdMI--VcHXH0HYw_AAAA__8DAOVTn9I");
    }

    [Fact]
    public async Task Package_DockerRunner_MermaidSvg()
    {
        var (exitCode, stdOut, stdErr) = await nugraph.RunAsync("DockerRunner", "--format", "mmd.svg", "--log", "Warning", "--no-browser");

        exitCode.Should().Be(0);
        stdErr.Should().BeEmpty();
        stdOut.Should().HaveCount(3);
        stdOut[0].Should().Be("Generating dependency graph for DockerRunner");
        stdOut[1].Should().Be("Generating dependency graph for DockerRunner 1.0.0-beta.2 (netstandard2.0)");
        stdOut[2].Should().Be("https://mermaid.ink/svg/pako:eNqUlW-PmkAQxr_KhsslbaIL_rlLyrv2NE2Ta9Oc3puLb9ZlkI2wS3eHqiF-9y6gLQpSjjcCM88z82MGzB2uAnB8ZzgcriQKjMEnM0hBBiD5gWw0SyOiQjJTfAv6JZMSNBlRj3rDNSCjY_JBAhpkMmA6GFPv40qWXit5f0--gk1nCAFZH0iEmBrfdTcCo2xNuUpcb88hcGVWlik0Vb3nl-Kcx8yYGYREK4XEoFZbGO5EgJE_Tfe1eAAhy2IkoYhjn_3KWMK0kDCoJP6d533yHkcDrmKl_btJeRQF6kx5Xr86HollyDxvMiffBdfKqBDpFx7Tz-Yg-TeJoEPGwaxkZ_ify-JgEBK6jDSwQMgNXTKzNXS-R5BGKGmdfsyXi9NzpM9irZk-5HnLzfbmbOKT0kB_xgxDpRNr-N-ajfYsPYoE6JNKUhGDXoD-LSwHfZWGhfB3KpfrUMynNXAaTBETfHsZKwqPx-ed2O121K4BIFV646aMb9kGjFtXuPWtq9Qnk5u7WYXPrXVP6qrX7uRezXdauA9FnxcY3SVr-U2gxvBv4zRT3wnTMLCDGd1EaZarZZ9BWpb8Yqna4leAbSm9wFqErv2M0ckFUJt9LevcaL_X6Lr3nqpeOP283Cl9uCLs2URNeAXd8Zlp5-0QvAf1tk1JOW2j7Chd06ykM3AS0AkTgf2LzKu7GEEC1alf_Zzxyouj1aRMvimVOD7qDI5_AAAA__8DAKzpnW0");
    }

    [Fact]
    public async Task Package_DockerRunner_GraphvizSvg()
    {
        var (exitCode, stdOut, stdErr) = await nugraph.RunAsync("DockerRunner", "--format", "dot.svg", "--log", "Warning", "--no-browser");

        exitCode.Should().Be(0);
        stdErr.Should().BeEmpty();
        stdOut.Should().HaveCount(3);
        stdOut[0].Should().Be("Generating dependency graph for DockerRunner");
        stdOut[1].Should().Be("Generating dependency graph for DockerRunner 1.0.0-beta.2 (netstandard2.0)");
        stdOut[2].Should().Be("https://kroki.io/graphviz/svg/eNqtU1Fr2zAQfs-vONSXDRzJadPBBhlsbRmFbowmfQp7OEtnW8SWPElZYkb_-2Sn7UKTpSnML_adT9_33XenE_hChhwGUpC1UIbQ-A9CFDqUy4xLW4t0LUkJsywcNuVgoPTm4_cAwKFZKO0mN7cxqDCjasIuqSGjyMgW-kKwOVxauSB3uzSRCUY85ekwo4D8FN4YCj6gUejUKU_fskFEMlYRzCG3JhisCSbAplRYgrvrBDwaP_TkdM5iUGLT_c_sOgahrbog11VFKunf0lbWxRz-XGKNThtK4DHHTtL0ffpuFHE6qr_ps_5h8KMTw7bFMxh-BPZVS2e9zQP_LCv-ybdGXptALkdJnnVnDlf0INPWB6r5rHSESpuCz9AvPL9aBzJeW7MB-nY1mz7Yw2905tC1zzXEkgvriH-vMOTW1ZuDL8Nvq4jdBV0Tv7B1oytyU3K_dJTK74zHnNgeI-ZP5pe0xsKaBOLgV1qFMubGCUTmfMIeF2q1WvG4QxS4dYVoUC6wIC-2McX2ZnTuv2zk_DiWgyjivKPd4du19fVsOxixxdET197Z_h9b90CLeL342QP1kWM_tuPj4MSYnz9XcGg_X0f-b6Sed9zx3g_-ALipqjY");
    }
}
