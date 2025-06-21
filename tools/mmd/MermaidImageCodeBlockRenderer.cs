using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Markdig.Renderers.Roundtrip;
using Markdig.Syntax;
using nugraph;

namespace mmd;

internal partial class MermaidImageCodeBlockRenderer(IReadOnlyDictionary<string, Uri> titleMap) : CodeBlockRenderer
{
    private int _n;

    public List<string> Replacements { get; } = [];

    protected override void Write(RoundtripRenderer renderer, CodeBlock codeBlock)
    {
        if (codeBlock is FencedCodeBlock fencedCodeBlock && fencedCodeBlock.Info?.Equals("mermaid", StringComparison.OrdinalIgnoreCase) == true)
        {
            _n++;
            renderer.RenderLinesBefore(codeBlock);
            renderer.Write(codeBlock.TriviaBefore);

            var text = fencedCodeBlock.Lines.ToString();
            var titleMatch = TitleRegex().Match(text);
            var title = titleMatch.Success ? titleMatch.Groups[1].Value : "";
            Replacements.Add(title == "" ? $"Mermaid diagram #{_n}" : title);
            if (!titleMap.TryGetValue(title, out var url))
            {
                // Requires [Remove the allowlist for allowed image domains in READMEs](https://github.com/NuGet/NuGetGallery/issues/10198) to be resolved first in order to use mermaid.ink images
                var data = Encoding.UTF8.GetBytes(text);
                url = GraphService.GetUri(data, OnlineService.MermaidInkSvg);
            }

            renderer.WriteLine($"![{title}]({url})");

            renderer.Write(codeBlock.TriviaAfter);
            renderer.RenderLinesAfter(codeBlock);
        }
        else
        {
            base.Write(renderer, codeBlock);
        }
    }

    [GeneratedRegex(@"^title:\s*(.*)", RegexOptions.Multiline)]
    private static partial Regex TitleRegex();
}