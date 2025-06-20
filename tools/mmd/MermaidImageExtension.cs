using System;
using System.Collections.Generic;
using Markdig;
using Markdig.Renderers;
using Markdig.Renderers.Roundtrip;
using Markdig.Renderers.Roundtrip.Inlines;

namespace mmd;

internal class MermaidImageExtension(Uri baseUri, IReadOnlyDictionary<string, Uri> titleMap) : IMarkdownExtension
{
    public void Setup(MarkdownPipelineBuilder pipeline)
    {
    }

    public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
    {
        if (renderer is RoundtripRenderer)
        {
            renderer.ObjectRenderers.Replace<CodeBlockRenderer>(new MermaidImageCodeBlockRenderer(titleMap));
            renderer.ObjectRenderers.Replace<LinkInlineRenderer>(new FullUrlLinkInlineRenderer(baseUri));
        }
    }
}