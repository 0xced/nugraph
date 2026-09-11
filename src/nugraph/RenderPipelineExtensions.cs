using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Spectre.Console.Rendering;

namespace nugraph;

internal static class RenderPipelineExtensions
{
    /// <summary>
    /// Aborts any current live status/progress rendering.
    /// </summary>
    /// <remarks>
    /// This method performs operations that are skipped when not properly canceled inside Progress.StartAsync(), i.e.,
    /// 1. Disposal of RenderHookScope => _console.Pipeline.Detach(_hook);
    /// 2. The code in the "finally" block => renderer.Completed(AutoClear);
    /// </remarks>
    public static void AbortLiveRendering(this RenderPipeline pipeline)
    {
        foreach (var renderHook in pipeline.GetRenderHooks())
        {
            pipeline.Detach(renderHook);
            var completed = renderHook.GetType().GetMethod("Completed", BindingFlags.Instance | BindingFlags.Public, types: [typeof(bool)]);
            completed?.Invoke(renderHook, [true]);
        }
    }

    private static IEnumerable<IRenderHook> GetRenderHooks(this RenderPipeline pipeline) => [.. GetHooks(pipeline)];

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_hooks")]
    private static extern ref readonly List<IRenderHook> GetHooks(RenderPipeline pipeline);
}