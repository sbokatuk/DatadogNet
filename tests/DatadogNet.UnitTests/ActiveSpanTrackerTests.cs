using DatadogNet;
using Xunit;

namespace DatadogNet.UnitTests;

/// <summary>
/// Covers the active-span bookkeeping that both platform tracers delegate to.
/// </summary>
/// <remarks>
/// The nesting and restore behaviour is identical on Android and iOS because it lives here rather
/// than in either platform head — so it is worth pinning down once, in the one place where it can be
/// tested without an SDK.
/// </remarks>
public class ActiveSpanTrackerTests
{
    [Fact]
    public void Reports_nothing_active_to_begin_with() => Assert.Null(ActiveSpanTracker.Current);

    [Fact]
    public void Makes_a_span_active_and_restores_on_dispose()
    {
        var span = new FakeSpan();

        using (ActiveSpanTracker.Activate(span, nativeScope: null))
        {
            Assert.Same(span, ActiveSpanTracker.Current);
        }

        Assert.Null(ActiveSpanTracker.Current);
    }

    [Fact]
    public void Restores_the_outer_span_rather_than_clearing()
    {
        // The case a naive implementation gets wrong: an inner scope closing should hand the thread
        // back to its parent, not leave it with nothing - otherwise every span started after a
        // nested one ends up unparented.
        var outer = new FakeSpan();
        var inner = new FakeSpan();

        using (ActiveSpanTracker.Activate(outer, nativeScope: null))
        {
            using (ActiveSpanTracker.Activate(inner, nativeScope: null))
            {
                Assert.Same(inner, ActiveSpanTracker.Current);
            }

            Assert.Same(outer, ActiveSpanTracker.Current);
        }

        Assert.Null(ActiveSpanTracker.Current);
    }

    [Fact]
    public void Disposes_the_native_scope_exactly_once()
    {
        var native = new CountingDisposable();
        var scope = ActiveSpanTracker.Activate(new FakeSpan(), native);

        scope.Dispose();
        scope.Dispose();
        scope.Dispose();

        // Android's native scope throws if closed twice, and a using block inside a try/finally that
        // also disposes is not an exotic shape.
        Assert.Equal(1, native.Disposals);
        Assert.Null(ActiveSpanTracker.Current);
    }

    [Fact]
    public void Finishing_the_active_span_deactivates_it()
    {
        // Both SDKs treat finishing as deactivating, so a caller who calls Finish() without ever
        // disposing the scope must not leave a finished span reported as active.
        var span = new FakeSpan();
        ActiveSpanTracker.Activate(span, nativeScope: null);

        ActiveSpanTracker.Finished(span);

        Assert.Null(ActiveSpanTracker.Current);
    }

    [Fact]
    public void Finishing_an_inner_span_does_not_deactivate_an_unrelated_one()
    {
        var active = new FakeSpan();
        var other = new FakeSpan();

        using (ActiveSpanTracker.Activate(active, nativeScope: null))
        {
            ActiveSpanTracker.Finished(other);

            Assert.Same(active, ActiveSpanTracker.Current);
        }
    }

    [Fact]
    public async Task Does_not_leak_across_threads()
    {
        // [ThreadStatic] rather than AsyncLocal, deliberately: the native scope managers are
        // thread-local, and reporting a span as active on a thread where the SDK's own stack is
        // empty would orphan every span started from it. This is that decision, asserted.
        var span = new FakeSpan();

        using (ActiveSpanTracker.Activate(span, nativeScope: null))
        {
            Assert.Same(span, ActiveSpanTracker.Current);

            var onAnotherThread = await Task.Factory.StartNew(
                () => ActiveSpanTracker.Current,
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);

            Assert.Null(onAnotherThread);
        }
    }

    private sealed class CountingDisposable : IDisposable
    {
        internal int Disposals { get; private set; }

        public void Dispose() => Disposals++;
    }

    private sealed class FakeSpan : IDatadogSpan
    {
        public string TraceId => "00000000000000000000000000000000";

        public string SpanId => "0";

        public void SetTag(string key, string value)
        {
        }

        public void SetTag(string key, double value)
        {
        }

        public void SetTag(string key, bool value)
        {
        }

        public void SetError(Exception exception)
        {
        }

        public void SetError(string kind, string message, string? stack = null)
        {
        }

        public void Log(IReadOnlyDictionary<string, object?> fields)
        {
        }

        public IDisposable Activate() => ActiveSpanTracker.Activate(this, nativeScope: null);

        public void Finish() => ActiveSpanTracker.Finished(this);

        public void Dispose() => Finish();
    }
}
