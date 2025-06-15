using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;
using CliWrap;

namespace nugraph;

internal sealed class JsonPipeTarget<T>(JsonTypeInfo<T> jsonTypeInfo) : PipeTarget
{
    private T? _result;
    private ExceptionDispatchInfo? _exceptionDispatchInfo;

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Rethrown through ExceptionDispatchInfo")]
    public override async Task CopyFromAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        try
        {
            _result = await JsonSerializer.DeserializeAsync(stream, jsonTypeInfo, cancellationToken);
        }
        catch (Exception exception)
        {
            _exceptionDispatchInfo = ExceptionDispatchInfo.Capture(exception);
        }
    }

    public T? Result
    {
        get
        {
            _exceptionDispatchInfo?.Throw();
            return _result;
        }
    }
}