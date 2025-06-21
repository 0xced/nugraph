using System;
using System.Collections.Generic;
using Markdig.Renderers.Roundtrip;
using Markdig.Renderers.Roundtrip.Inlines;
using Markdig.Syntax.Inlines;

namespace mmd;

internal class AbsoluteUrlLinkInlineRenderer(Uri baseUri) : LinkInlineRenderer
{
    public List<string> Replacements { get; } = [];

    protected override void Write(RoundtripRenderer renderer, LinkInline link)
    {
        if (link.IsImage)
        {
            renderer.Write('!');
        }
        // link text
        renderer.Write('[');
        renderer.WriteChildren(link);
        renderer.Write(']');

        if (link.Label != null)
        {
            if (link.LocalLabel is LocalLabel.Local or LocalLabel.Empty)
            {
                renderer.Write('[');
                if (link.LocalLabel == LocalLabel.Local)
                {
                    renderer.Write(link.LabelWithTrivia);
                }
                renderer.Write(']');
            }
        }
        else
        {
            if (link.Url != null)
            {
                renderer.Write('(');
                renderer.Write(link.TriviaBeforeUrl);
                if (link.UrlHasPointyBrackets)
                {
                    renderer.Write('<');
                }
                if (Uri.TryCreate(link.UnescapedUrl.ToString(), UriKind.Relative, out var relative))
                {
                    var uri = new Uri(baseUri, relative).AbsoluteUri;
                    renderer.Write(uri);
                    Replacements.Add(uri);
                }
                else
                {
                    renderer.Write(link.UnescapedUrl);
                }
                if (link.UrlHasPointyBrackets)
                {
                    renderer.Write('>');
                }
                renderer.Write(link.TriviaAfterUrl);

                if (!string.IsNullOrEmpty(link.Title))
                {
                    var open = link.TitleEnclosingCharacter;
                    var close = link.TitleEnclosingCharacter;
                    if (link.TitleEnclosingCharacter == '(')
                    {
                        close = ')';
                    }
                    renderer.Write(open);
                    renderer.Write(link.UnescapedTitle);
                    renderer.Write(close);
                    renderer.Write(link.TriviaAfterTitle);
                }

                renderer.Write(')');
            }
        }
    }
}