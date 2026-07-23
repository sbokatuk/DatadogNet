namespace DatadogNet;

/// <summary>
/// Remembers which span is active on the current thread.
/// </summary>
/// <remarks>
/// Both native tracers have an active-span notion of their own, but neither exposes it in a form
/// the façade can read back uniformly: dd-sdk-android's <c>Tracer.activeSpan()</c> returns a
/// <c>io.opentracing.Span</c>, which is not the <see cref="IDatadogSpan"/> the caller was handed,
/// and <c>OTTracer</c> on iOS has no equivalent member at all. So the mapping from "the span the
/// SDK considers active" back to "the wrapper you can call
/// <see cref="IDatadogSpan.SetTag(string, string)"/> on" is kept here.
/// <para>
/// <c>[ThreadStatic]</c> rather than <see cref="System.Threading.AsyncLocal{T}"/>, deliberately.
/// The native scope managers are thread-local, so an <see cref="System.Threading.AsyncLocal{T}"/>
/// here would report a span as active on a thread where the SDK does not — a continuation resumed
/// on the thread pool would find <see cref="IDatadogTracer.ActiveSpan"/> populated while the
/// tracer's own stack was empty, and spans started from it would be parented here and orphaned
/// there. Matching the SDKs' own semantics, and documenting that activation does not flow across
/// an <see langword="await"/>, is the honest option.
/// </para>
/// </remarks>
internal static class ActiveSpanTracker
{
    [ThreadStatic]
    private static IDatadogSpan? current;

    /// <summary>The span active on this thread, if any.</summary>
    internal static IDatadogSpan? Current => current;

    /// <summary>
    /// Makes <paramref name="span"/> active until the returned scope is disposed, restoring
    /// whatever was active before.
    /// </summary>
    internal static IDisposable Activate(IDatadogSpan span, IDisposable? nativeScope)
    {
        var previous = current;
        current = span;
        return new Scope(previous, nativeScope);
    }

    /// <summary>Forgets <paramref name="span"/> if it is the one currently active.</summary>
    /// <remarks>
    /// Called when a span finishes without its scope having been disposed, which both SDKs treat as
    /// deactivating it.
    /// </remarks>
    internal static void Finished(IDatadogSpan span)
    {
        if (ReferenceEquals(current, span))
        {
            current = null;
        }
    }

    private sealed class Scope(IDatadogSpan? previous, IDisposable? nativeScope) : IDisposable
    {
        private bool disposed;

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            current = previous;
            nativeScope?.Dispose();
        }
    }
}
