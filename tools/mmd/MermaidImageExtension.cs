using Markdig;
using Markdig.Renderers;
using Markdig.Renderers.Roundtrip;

namespace mmd;

internal class MermaidImageExtension : IMarkdownExtension
{
    public void Setup(MarkdownPipelineBuilder pipeline)
    {
    }

    public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
    {
        if (renderer is RoundtripRenderer)
        {
            renderer.ObjectRenderers.Replace<CodeBlockRenderer>(new MermaidImageCodeBlockRenderer());
        }
    }
}