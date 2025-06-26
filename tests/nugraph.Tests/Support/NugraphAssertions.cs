using System;
using System.Buffers.Text;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using AwesomeAssertions.Primitives;

namespace nugraph.Tests;

public static class NugraphResultExtensions
{
    public static NugraphAssertions Should(this NugraphResult instance)
    {
        return new NugraphAssertions(instance, AssertionChain.GetOrCreate());
    }
}

public class NugraphAssertions(NugraphResult instance, AssertionChain chain) :
    ReferenceTypeAssertions<NugraphResult, NugraphAssertions>(instance, chain)
{
    private readonly AssertionChain _chain = chain;

    protected override string Identifier => "result";

    /// <summary>
    /// Asserts that the nugraph result matches the exit code, stdout and stderr content.
    /// </summary>
    /// <param name="exitCode">The expected exit code.</param>
    /// <param name="stdOutPattern">The expected pattern that must be written on stdout or an empty string to assert that nothing was written.</param>
    /// <param name="stdErrPattern">The expected pattern that must be written on stderr or an empty string to assert that nothing was written.</param>
    /// <param name="because">
    /// A formatted phrase as is supported by <see cref="string.Format(string,object[])" /> explaining why the assertion is needed. If the phrase does not start with the word <i>because</i>, it is prepended automatically.
    /// </param>
    /// <param name="becauseArgs">
    /// Zero or more objects to format using the placeholders in <paramref name="because" />.
    /// </param>
    [CustomAssertion]
    public AndConstraint<NugraphAssertions> Match(int exitCode = 0, string stdOutPattern = "", string stdErrPattern = "", [StringSyntax("CompositeFormat")] string because = "", params object[] becauseArgs)
    {
        string[] failures = [];

        _chain
            .Given(() => Subject)
            .ForCondition(result =>
            {
                using var scope = new AssertionScope();

                result.ExitCode.Should().Be(exitCode, because, becauseArgs);

                if (string.IsNullOrEmpty(stdOutPattern))
                    result.StdOut.Should().BeEmpty(because, becauseArgs);
                else
                    result.StdOut.Should().MatchEquivalentOf(stdOutPattern, opt => opt.IgnoringNewlineStyle(), because, becauseArgs);

                if (string.IsNullOrEmpty(stdErrPattern))
                    result.StdErr.Should().BeEmpty(because, becauseArgs);
                else
                    result.StdErr.Should().MatchEquivalentOf(stdErrPattern, opt => opt.IgnoringNewlineStyle(), because, becauseArgs);

                failures = scope.Discard();

                return failures.Length == 0;
            })
            .FailWith(string.Join(Environment.NewLine, failures.Select(e => e.Replace("{", "{{").Replace("}", "}}"))));

        return new AndConstraint<NugraphAssertions>(this);
    }

    [CustomAssertion]
    public AndConstraint<NugraphAssertions> UrlHasDiagram(string expectedDiagram, [StringSyntax("CompositeFormat")] string because = "", params object[] becauseArgs)
    {
        string[] failures = [];

        _chain
            .Given(() => Subject)
            .ForCondition(result =>
            {
                var url = new Uri(result.StdOut.Split('\n').Last());
                var urlPart = url.Fragment.Length > 0 ? url.Fragment : url.Segments.Last();
                var colonIndex = urlPart.IndexOf(':');
                var data = Base64Url.DecodeFromChars(urlPart.AsSpan(Range.StartAt(colonIndex + 1)));
                using var memoryStream = new MemoryStream(data);
                using var zlibStream = new ZLibStream(memoryStream, CompressionMode.Decompress, leaveOpen: true);
                string? diagram;
                if (url.Host is "mermaid.live" or "mermaid.ink")
                {
                    using var json = JsonDocument.Parse(zlibStream);
                    diagram = json.RootElement.GetProperty("code").GetString()?.TrimEnd();
                }
                else
                {
                    using var reader = new StreamReader(zlibStream, leaveOpen: true);
                    diagram = reader.ReadToEnd().TrimEnd();
                }

                using var scope = new AssertionScope();
                diagram.Should().BeEquivalentTo(expectedDiagram, opt => opt.IgnoringNewlineStyle(), because, becauseArgs);
                failures = scope.Discard();

                return failures.Length == 0;
            })
            .FailWith(string.Join(Environment.NewLine, failures.Select(e => e.Replace("{", "{{").Replace("}", "}}"))));

        return new AndConstraint<NugraphAssertions>(this);
    }
}
