using System;
using System.Text;
using Markdig.Renderers.Roundtrip;
using Markdig.Syntax;
using nugraph;

namespace mmd;

internal class MermaidImageCodeBlockRenderer : CodeBlockRenderer
{
    protected override void Write(RoundtripRenderer renderer, CodeBlock codeBlock)
    {
        if (codeBlock is FencedCodeBlock fencedCodeBlock && fencedCodeBlock.Info?.Equals("mermaid", StringComparison.OrdinalIgnoreCase) == true)
        {
            renderer.RenderLinesBefore(codeBlock);
            renderer.Write(codeBlock.TriviaBefore);

            var text = fencedCodeBlock.Lines.ToString();
            var data = Encoding.UTF8.GetBytes(text);
            var url = GraphService.GetUri(data, OnlineService.MermaidInkSvg);
            renderer.WriteLine($"![]({url})");

            renderer.Write(codeBlock.TriviaAfter);
            renderer.RenderLinesAfter(codeBlock);
        }
        else
        {
            base.Write(renderer, codeBlock);
        }
    }
}