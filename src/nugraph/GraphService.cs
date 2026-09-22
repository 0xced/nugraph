using System;
using System.Buffers.Text;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text.Json;

namespace nugraph;

public enum OnlineService
{
    MermaidLiveView,
    MermaidLiveEdit,
    MermaidInkSvg,
    MermaidInkPng,
    MermaidInkJpg,
    MermaidInkWebp,
    MermaidKrokiPng,
    MermaidKrokiSvg,
    GraphvizEdotor,
    GraphvizKrokiSvg,
    GraphvizKrokiPng,
    GraphvizKrokiJpg,
    GraphvizKrokiPdf,
}

internal static class GraphService
{
    public static Uri GetUri(ReadOnlySpan<byte> data, OnlineService service, string layout)
    {
        return service switch
        {
            OnlineService.MermaidLiveView => GetUri(data, "https://mermaid.live/view#pako:{0}", layout),
            OnlineService.MermaidLiveEdit => GetUri(data, "https://mermaid.live/edit#pako:{0}", layout),
            OnlineService.MermaidInkSvg => GetUri(data, "https://mermaid.ink/svg/pako:{0}", layout),
            OnlineService.MermaidInkPng => GetUri(data, "https://mermaid.ink/img/pako:{0}?type=png", layout),
            OnlineService.MermaidInkJpg => GetUri(data, "https://mermaid.ink/img/pako:{0}?type=jpeg", layout),
            OnlineService.MermaidInkWebp => GetUri(data, "https://mermaid.ink/img/pako:{0}?type=webp", layout),
            OnlineService.MermaidKrokiPng => GetUri(data, "https://kroki.io/mermaid/png/{0}", layout: null),
            OnlineService.MermaidKrokiSvg => GetUri(data, "https://kroki.io/mermaid/svg/{0}", layout: null),
            OnlineService.GraphvizEdotor => GetUri(data, "https://edotor.net/#deflate:{0}", layout: null),
            OnlineService.GraphvizKrokiSvg => GetUri(data, "https://kroki.io/graphviz/svg/{0}", layout: null),
            OnlineService.GraphvizKrokiPng => GetUri(data, "https://kroki.io/graphviz/png/{0}", layout: null),
            OnlineService.GraphvizKrokiJpg => GetUri(data, "https://kroki.io/graphviz/jpeg/{0}", layout: null),
            OnlineService.GraphvizKrokiPdf => GetUri(data, "https://kroki.io/graphviz/pdf/{0}", layout: null),
            _ => throw new ArgumentOutOfRangeException(nameof(service), service, $"The value of argument '{nameof(service)}' ({service}) is invalid for enum type '{nameof(OnlineService)}'."),
        };
    }

    private static Uri GetUri(ReadOnlySpan<byte> data, string template, string? layout)
    {
        using var memoryStream = new MemoryStream(capacity: 2048);
        using (var zlibStream = new ZLibStream(memoryStream, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            if (layout != null)
            {
                // See https://github.com/mermaid-js/mermaid-live-editor/blob/dc72838036719637f3947a7c16c0cbbdeba0d73b/src/lib/types.d.ts#L21-L31
                // And https://github.com/mermaid-js/mermaid-live-editor/blob/dc72838036719637f3947a7c16c0cbbdeba0d73b/src/lib/util/state.ts#L10-L23
                using var writer = new Utf8JsonWriter(zlibStream);
                writer.WriteStartObject();
                writer.WriteString("code"u8, data);
                writer.WritePropertyName("mermaid"u8);
                writer.WriteStartObject();
                writer.WriteString("theme"u8, "default"u8); // https://mermaid.js.org/config/theming.html#available-themes
                writer.WriteString("layout"u8, layout); // https://mermaid.js.org/config/layouts.html#supported-layouts
                writer.WriteEndObject();
                writer.WriteBoolean("panZoom"u8, true);
                writer.WriteEndObject();
            }
            else
            {
                zlibStream.Write(data);
            }
        }

        // See https://github.com/mermaid-js/mermaid-live-editor/discussions/1291
#if USE_PADDING
        var payload = Convert.ToBase64String(memoryStream.AsSpan()).Replace("/", "_").Replace("+", "-");
#else
        var payload = Base64Url.EncodeToString(memoryStream.AsSpan());
#endif
        return new Uri(string.Format(CultureInfo.InvariantCulture, template, payload));
    }
}