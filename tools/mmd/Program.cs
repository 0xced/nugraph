using System;
using System.Collections.Generic;
using System.IO;
using Markdig;
using Markdig.Renderers.Roundtrip;
using mmd;

if (args.Length < 1)
{
    Console.Error.WriteLine($"Usage: {Path.GetFileName(Environment.GetCommandLineArgs()[0])} <markdown_input> [markdown_output]");
    return 64;
}

try
{
    var input = Path.GetFullPath(args[0]);
    var output = args.Length > 1 ? Path.GetFullPath(args[1]) : null;
    var markdown = File.ReadAllText(input);

    using var writer = output != null ? new StreamWriter(new FileStream(output, FileMode.Create, FileAccess.Write)) : Console.Out;
    var renderer = new RoundtripRenderer(writer);
    var titleMap = new Dictionary<string, Uri>
    {
        ["Dependency graph of Microsoft.Extensions.Logging.Console 9.0.6 (net8.0)"] = new("https://raw.githubusercontent.com/0xced/nugraph/fab28a88a9edd87ef58126c1b53ff8cd38708bb3/resources/Microsoft.Extensions.Logging.Console.svg"),
    };
    var pipeline = new MarkdownPipelineBuilder().EnableTrackTrivia().Use(new MermaidImageExtension(titleMap)).Build();
    Markdown.Convert(markdown, renderer, pipeline);

    if (output != null)
    {
        Console.WriteLine($"Replaced mermaid code blocks with images in {new Uri(output)}");
    }

    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception);
    return 70;
}