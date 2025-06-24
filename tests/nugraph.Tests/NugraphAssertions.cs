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
    /// Asserts that the nugraph result is successful.
    /// <list type="bullet">
    /// <item>Ensures that exit code is 0.</item>
    /// <item>Ensures that stderr is empty.</item>
    /// <item>Ensures that stdout matches the provided pattern.</item>
    /// </list>
    /// </summary>
    /// <param name="stdOutPattern">The expected pattern that must be written on stdout.</param>
    /// <param name="because">
    /// A formatted phrase as is supported by <see cref="string.Format(string,object[])" /> explaining why the assertion is needed. If the phrase does not start with the word <i>because</i>, it is prepended automatically.
    /// </param>
    /// <param name="becauseArgs">
    /// Zero or more objects to format using the placeholders in <paramref name="because" />.
    /// </param>
    [CustomAssertion]
    public AndConstraint<NugraphAssertions> Succeed(string stdOutPattern, [StringSyntax("CompositeFormat")] string because = "", params object[] becauseArgs)
    {
        string[] failures = [];

        _chain
            .Given(() => Subject)
            .ForCondition(result =>
            {
                using var scope = new AssertionScope();
                result.ExitCode.Should().Be(0, because, becauseArgs);
                result.StdErr.Should().BeEmpty(because, becauseArgs);
                result.StdOut.Should().MatchEquivalentOf(stdOutPattern, opt => opt.IgnoringNewlineStyle(), because, becauseArgs);
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

    /// <summary>
    /// Asserts that the nugraph result is a failure.
    /// <list type="bullet">
    /// <item>Ensures that exit code is <paramref name="failCode"/>.</item>
    /// <item>Ensures that stderr matches the provided pattern.</item>
    /// </list>
    /// </summary>
    /// <param name="failCode">The expected exit code.</param>
    /// <param name="stdErrPattern">The expected pattern that must be written on stderr.</param>
    /// <param name="because">
    /// A formatted phrase as is supported by <see cref="string.Format(string,object[])" /> explaining why the assertion is needed. If the phrase does not start with the word <i>because</i>, it is prepended automatically.
    /// </param>
    /// <param name="becauseArgs">
    /// Zero or more objects to format using the placeholders in <paramref name="because" />.
    /// </param>
    [CustomAssertion]
    public AndConstraint<NugraphAssertions> Fail(int failCode, string stdErrPattern, string because = "", params object[] becauseArgs)
    {
        string[] failures = [];

        _chain
            .Given(() => Subject)
            .ForCondition(result =>
            {
                using var scope = new AssertionScope();
                result.ExitCode.Should().Be(failCode, because, becauseArgs);
                result.StdErr.Should().MatchEquivalentOf(stdErrPattern, opt => opt.IgnoringNewlineStyle(), because, becauseArgs);
                failures = scope.Discard();

                return failures.Length == 0;
            })
            .FailWith(string.Join(Environment.NewLine, failures.Select(e => e.Replace("{", "{{").Replace("}", "}}"))));

        return new AndConstraint<NugraphAssertions>(this);
    }
}
