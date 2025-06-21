using System;
using System.Collections.Generic;
using Markdig;
using Markdig.Renderers;
using Markdig.Renderers.Roundtrip;
using Markdig.Renderers.Roundtrip.Inlines;

namespace mmd;

internal class NuGetReadmeExtension(Uri baseUri, IReadOnlyDictionary<string, Uri> titleMap) : IMarkdownExtension
{
    private readonly MermaidImageCodeBlockRenderer _codeBlockRenderer = new(titleMap);
    private readonly AbsoluteUrlLinkInlineRenderer _linkInlineRenderer = new(baseUri);

    public void Setup(MarkdownPipelineBuilder pipeline)
    {
    }

    public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
    {
        if (renderer is RoundtripRenderer)
        {
            renderer.ObjectRenderers.Replace<CodeBlockRenderer>(_codeBlockRenderer);
            renderer.ObjectRenderers.Replace<LinkInlineRenderer>(_linkInlineRenderer);
        }
    }

    public List<string> CodeBlockReplacements => _codeBlockRenderer.Replacements;
    public List<string> AbsoluteUrlReplacements => _linkInlineRenderer.Replacements;
}