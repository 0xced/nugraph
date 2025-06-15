using System.Buffers.Text;
using System.IO.Compression;
using System.Text.Json;
using TextCopy;

var uri = new Uri(ClipboardService.GetText() ?? "about:blank");
var pako = Base64Url.DecodeFromChars(uri.Fragment.AsSpan()[6..]);

using var memoryStream = new MemoryStream(pako);
using var zlibStream = new ZLibStream(memoryStream, CompressionMode.Decompress, leaveOpen: true);
using var json = JsonDocument.Parse(zlibStream);
var pretty = JsonSerializer.Serialize(json, new JsonSerializerOptions { WriteIndented = true });
Console.WriteLine(pretty);