using System.IO;
using System.Threading.Tasks;
using AwesomeAssertions;
using AwesomeAssertions.Execution;

namespace nugraph.Tests;

public abstract class NugraphTests(Nugraph nugraph)
{
    [Test]
    public async Task Diagnose()
    {
        var (exitCode, stdOut, stdErr) = await nugraph.RunAsync(["--diagnose"]);

        await File.WriteAllTextAsync($"{nugraph.GetType().Name}.diagnostics.txt", stdOut);

        using var _ = new AssertionScope();
        exitCode.Should().Be(0);
        stdErr.Should().BeEmpty();
        stdOut.Should().NotBeEmpty();
    }

    [Test]
    public async Task Version()
    {
        var (exitCode, stdOut, stdErr) = await nugraph.RunAsync(["--version"]);

        using var _ = new AssertionScope();
        exitCode.Should().Be(0);
        stdErr.Should().BeEmpty();
        stdOut.Should().Be(nugraph.Version);
    }

    [Test]
    public async Task Help()
    {
        var (exitCode, stdOut, stdErr) = await nugraph.RunAsync(["--help"]);

        using var _ = new AssertionScope();
        exitCode.Should().Be(0);
        stdErr.Should().BeEmpty();
        stdOut.Should().NotBeEmpty();
    }

    [Test]
    public async Task Package_Serilog()
    {
        var (exitCode, stdOut, stdErr) = await nugraph.RunAsync(["Serilog"]);

        using var _ = new AssertionScope();
        exitCode.Should().Be(0);
        stdErr.Should().BeEmpty();
        stdOut.Should().Match("""
                           Generating dependency graph for Serilog
                           Generating dependency graph for Serilog *
                           https://mermaid.live/view#pako:*
                           """);
    }

    [Test]
    public async Task Package_Serilog_430_net60()
    {
        var (exitCode, stdOut, stdErr) = await nugraph.RunAsync(["Serilog/4.3.0", "--framework", "net6.0"]);

        using var _ = new AssertionScope();
        exitCode.Should().Be(0);
        stdErr.Should().BeEmpty();
        stdOut.Should().Be("""
                           Generating dependency graph for Serilog 4.3.0
                           Generating dependency graph for Serilog 4.3.0 (net6.0)
                           https://mermaid.live/view#pako:eNpcUcFuhCAU_BXiZpM2UaG7m03KeZNeempvjRcWnkpEsPiMuzH-e1Vc05YLw8C8efMYIukURDxKkiSzqNEAJxdowCqw8k4KL5qSuJx8gtfGFeSUHlNGnizgOWXPmV10md3vyRtY8AJBkeudlIhNyyktNJbdNZWupuwmQVHbLSVnTaj9_jFjaUTbXiAn3jkkLXpXQdJrhSU_Nbdf9wpy0RkkuTaGi-9O1MJrC3GQ8B1jr-z8EktnnOe747Jmg7X_YVjBOG62W7bZ-z-3-s20ltVGZx1jh8MjZd_36RQMMHW-oI2QlSigpetjuswsKFbhn2kGKrNRHNXga6HV9B9DYLGEGgLkYXs0tBzGSdMI--VcHXH0HYw_AAAA__8DAOVTn9I
                           """);
    }

    [Test]
    public async Task Package_DockerRunner_MermaidSvg()
    {
        var (exitCode, stdOut, stdErr) = await nugraph.RunAsync(["DockerRunner", "--format", "mmd.svg"]);

        using var _ = new AssertionScope();
        exitCode.Should().Be(0);
        stdErr.Should().BeEmpty();
        stdOut.Should().Be("""
                           Generating dependency graph for DockerRunner
                           Generating dependency graph for DockerRunner 1.0.0-beta.2 (netstandard2.0)
                           https://mermaid.ink/svg/pako:eNqUlW-PmkAQxr_KhsslbaIL_rlLyrv2NE2Ta9Oc3puLb9ZlkI2wS3eHqiF-9y6gLQpSjjcCM88z82MGzB2uAnB8ZzgcriQKjMEnM0hBBiD5gWw0SyOiQjJTfAv6JZMSNBlRj3rDNSCjY_JBAhpkMmA6GFPv40qWXit5f0--gk1nCAFZH0iEmBrfdTcCo2xNuUpcb88hcGVWlik0Vb3nl-Kcx8yYGYREK4XEoFZbGO5EgJE_Tfe1eAAhy2IkoYhjn_3KWMK0kDCoJP6d533yHkcDrmKl_btJeRQF6kx5Xr86HollyDxvMiffBdfKqBDpFx7Tz-Yg-TeJoEPGwaxkZ_ify-JgEBK6jDSwQMgNXTKzNXS-R5BGKGmdfsyXi9NzpM9irZk-5HnLzfbmbOKT0kB_xgxDpRNr-N-ajfYsPYoE6JNKUhGDXoD-LSwHfZWGhfB3KpfrUMynNXAaTBETfHsZKwqPx-ed2O121K4BIFV646aMb9kGjFtXuPWtq9Qnk5u7WYXPrXVP6qrX7uRezXdauA9FnxcY3SVr-U2gxvBv4zRT3wnTMLCDGd1EaZarZZ9BWpb8Yqna4leAbSm9wFqErv2M0ckFUJt9LevcaL_X6Lr3nqpeOP283Cl9uCLs2URNeAXd8Zlp5-0QvAf1tk1JOW2j7Chd06ykM3AS0AkTgf2LzKu7GEEC1alf_Zzxyouj1aRMvimVOD7qDI5_AAAA__8DAKzpnW0
                           """);
    }

    [Test]
    public async Task Package_DockerRunner_GraphvizSvg()
    {
        var (exitCode, stdOut, stdErr) = await nugraph.RunAsync(["DockerRunner", "--format", "dot.svg"]);

        using var _ = new AssertionScope();
        exitCode.Should().Be(0);
        stdErr.Should().BeEmpty();
        stdOut.Should().Be("""
                           Generating dependency graph for DockerRunner
                           Generating dependency graph for DockerRunner 1.0.0-beta.2 (netstandard2.0)
                           https://kroki.io/graphviz/svg/eNqtU1Fr2zAQfs-vONSXDRzJadPBBhlsbRmFbowmfQp7OEtnW8SWPElZYkb_-2Sn7UKTpSnML_adT9_33XenE_hChhwGUpC1UIbQ-A9CFDqUy4xLW4t0LUkJsywcNuVgoPTm4_cAwKFZKO0mN7cxqDCjasIuqSGjyMgW-kKwOVxauSB3uzSRCUY85ekwo4D8FN4YCj6gUejUKU_fskFEMlYRzCG3JhisCSbAplRYgrvrBDwaP_TkdM5iUGLT_c_sOgahrbog11VFKunf0lbWxRz-XGKNThtK4DHHTtL0ffpuFHE6qr_ps_5h8KMTw7bFMxh-BPZVS2e9zQP_LCv-ybdGXptALkdJnnVnDlf0INPWB6r5rHSESpuCz9AvPL9aBzJeW7MB-nY1mz7Yw2905tC1zzXEkgvriH-vMOTW1ZuDL8Nvq4jdBV0Tv7B1oytyU3K_dJTK74zHnNgeI-ZP5pe0xsKaBOLgV1qFMubGCUTmfMIeF2q1WvG4QxS4dYVoUC6wIC-2McX2ZnTuv2zk_DiWgyjivKPd4du19fVsOxixxdET197Z_h9b90CLeL342QP1kWM_tuPj4MSYnz9XcGg_X0f-b6Sed9zx3g_-ALipqjY
                           """);
    }

    [Test]
    public async Task Package_DoesNotExist()
    {
        var (exitCode, stdOut, stdErr) = await nugraph.RunAsync(["DoesNotExist"]);

        using var _ = new AssertionScope();
        exitCode.Should().Be(66);
        stdOut.Should().Be("Generating dependency graph for DoesNotExist");
        stdErr.Should().Be("Package DoesNotExist was not found in nuget.org [https://api.nuget.org/v3/index.json]");
    }

    [Test]
    public async Task Project_nugraph_WorkingDirectory()
    {
        var (exitCode, stdOut, stdErr) = await nugraph.RunAsync(["-m", "gv"], workingDirectory: RepositoryDirectories.GetPath("src", "nugraph"));

        using var _ = new AssertionScope();
        exitCode.Should().Be(0);
        stdErr.Should().BeEmpty();
        stdOut.Should().Match("""
                              Generating dependency graph for nugraph
                              https://edotor.net/#deflate:*
                              """);
    }

    [Test]
    public async Task Project_nugraph_ExplicitDirectory()
    {
        var (exitCode, stdOut, stdErr) = await nugraph.RunAsync([RepositoryDirectories.GetPath("src", "nugraph"), "-m", "graphviz"]);

        using var _ = new AssertionScope();
        exitCode.Should().Be(0);
        stdErr.Should().BeEmpty();
        stdOut.Should().Match("""
                              Generating dependency graph for nugraph
                              https://edotor.net/#deflate:*
                              """);
    }

    [Test]
    public async Task Project_nugraph_ProjectFile()
    {
        var (exitCode, stdOut, stdErr) = await nugraph.RunAsync([RepositoryDirectories.GetPath("src", "nugraph", "nugraph.csproj"), "-m", "dot"]);

        using var _ = new AssertionScope();
        exitCode.Should().Be(0);
        stdErr.Should().BeEmpty();
        stdOut.Should().Match("""
                              Generating dependency graph for nugraph.csproj
                              https://edotor.net/#deflate:*
                              """);
    }

    [Test]
    public async Task Project_SolutionFile()
    {
        var (exitCode, stdOut, stdErr) = await nugraph.RunAsync([], workingDirectory: RepositoryDirectories.GetPath());

        using var _ = new AssertionScope();
        exitCode.Should().Be(65);
        stdOut.Should().Be("Generating dependency graph for nugraph");
        stdErr.Should().Be("""
                           Solution files are not supported.
                           Please run nugraph in a directory that contains a single project file or pass an explicit project file as the first argument.
                           """);
    }

    [Test]
    public async Task Project_NoProject()
    {
        var (exitCode, stdOut, stdErr) = await nugraph.RunAsync([], workingDirectory: RepositoryDirectories.GetPath("resources"));

        using var _ = new AssertionScope();
        exitCode.Should().Be(65);
        stdOut.Should().Be("Generating dependency graph for resources");
        stdErr.Should().Be("""
                           The current working directory does not contain a project file.
                           Please run nugraph in a directory that contains a single project file or pass an explicit project file as the first argument.
                           """);
    }
}