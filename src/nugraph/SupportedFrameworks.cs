using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Frameworks;

namespace nugraph;

public partial class SupportedFrameworks
{
    private readonly FileInfo _file;

    public static SupportedFrameworks Cache { get; } = new();

    private SupportedFrameworks()
    {
        _file = new FileInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "nugraph", "supportedFrameworks.cache"));
        _file.Directory?.Create();
    }

    public async Task<HashSet<NuGetFramework>?> FindAsync(string sdkPath, CancellationToken cancellationToken)
    {
        var cache = await ReadSupportedFrameworksCacheAsync(cancellationToken);
        return cache.GetValueOrDefault(sdkPath);
    }

    public async Task<HashSet<NuGetFramework>> SetAsync(string sdkPath, HashSet<NuGetFramework> supportedFrameworks, CancellationToken cancellationToken)
    {
        var cache = await ReadSupportedFrameworksCacheAsync(cancellationToken);
        cache[sdkPath] = supportedFrameworks;

        await using var stream = _file.Open(FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite);
        stream.SetLength(0);
        await JsonSerializer.SerializeAsync(stream, cache, FileCacheSerializerContext.Default.DictionaryStringHashSetNuGetFramework, cancellationToken);

        return supportedFrameworks;
    }

    private async Task<Dictionary<string, HashSet<NuGetFramework>>> ReadSupportedFrameworksCacheAsync(CancellationToken cancellationToken)
    {
        if (!_file.Exists)
        {
            return [];
        }

        await using var stream = _file.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        try
        {
            var cache = await JsonSerializer.DeserializeAsync(stream, FileCacheSerializerContext.Default.DictionaryStringHashSetNuGetFramework, cancellationToken: cancellationToken);
            return cache ?? [];
        }
        catch (JsonException)
        {
            // cache file is corrupted => delete it
            _file.Delete();
            return [];
        }
    }

    [JsonSerializable(typeof(Dictionary<string, HashSet<NuGetFramework>>))]
    [JsonSourceGenerationOptions(WriteIndented = true, Converters = [ typeof(NuGetFrameworkJsonConverter) ])]
    private sealed partial class FileCacheSerializerContext : JsonSerializerContext;

    private sealed class NuGetFrameworkJsonConverter : JsonConverter<NuGetFramework>
    {
        public override NuGetFramework Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var framework = reader.GetString();
            return NuGetFramework.Parse(framework ?? throw new JsonException());
        }

        public override void Write(Utf8JsonWriter writer, NuGetFramework value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.DotNetFrameworkName);
        }
    }
}