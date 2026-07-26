using Xunit;

namespace DatadogNet.UnitTests;

/// <summary>
/// The cancellable form of <see cref="IRumMonitor.GetCurrentSessionIdAsync()"/>.
/// </summary>
/// <remarks>
/// It is a default interface implementation, so the two things worth proving are the two halves
/// of that choice: an implementation that only knows the parameterless form still gets working
/// cancellation for free, and an answer that is already there beats a cancelled token — the
/// caller wanted the id, and it costs nothing to hand it over.
/// </remarks>
public class RumSessionIdCancellationTests
{
    [Fact]
    public async Task A_hung_lookup_can_be_abandoned()
    {
        IRumMonitor monitor = new HangingRumMonitor();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => monitor.GetCurrentSessionIdAsync(cancellation.Token));
    }

    [Fact]
    public async Task An_answer_that_is_already_there_wins_over_a_cancelled_token()
    {
        // The neutral no-op answers immediately; Task.WaitAsync hands a completed task straight
        // through without looking at the token, which is the behaviour a caller wants.
        var sessionId = await Datadog.Rum.GetCurrentSessionIdAsync(new CancellationToken(canceled: true));

        Assert.Null(sessionId);
    }

    /// <summary>Implements only the parameterless form, like any pre-existing fake.</summary>
    private sealed class HangingRumMonitor : IRumMonitor
    {
        public bool IsEnabled => false;

        public bool Debug { get; set; }

        public IRumViewScope StartView(
            string key,
            string? name = null,
            IReadOnlyDictionary<string, object?>? attributes = null) =>
            throw new NotSupportedException();

        public void StopView(string key, IReadOnlyDictionary<string, object?>? attributes = null)
        {
        }

        public void AddAction(RumActionType type, string name, IReadOnlyDictionary<string, object?>? attributes = null)
        {
        }

        public void StartAction(RumActionType type, string name, IReadOnlyDictionary<string, object?>? attributes = null)
        {
        }

        public void StopAction(RumActionType type, string? name = null, IReadOnlyDictionary<string, object?>? attributes = null)
        {
        }

        public void AddError(
            Exception exception,
            RumErrorSource source = RumErrorSource.Source,
            IReadOnlyDictionary<string, object?>? attributes = null)
        {
        }

        public void AddError(
            string message,
            RumErrorSource source = RumErrorSource.Source,
            string? stack = null,
            IReadOnlyDictionary<string, object?>? attributes = null)
        {
        }

        public void StartResource(
            string key,
            RumHttpMethod method,
            string url,
            IReadOnlyDictionary<string, object?>? attributes = null)
        {
        }

        public void StopResource(
            string key,
            int? statusCode = null,
            RumResourceKind kind = RumResourceKind.Native,
            long? size = null,
            IReadOnlyDictionary<string, object?>? attributes = null)
        {
        }

        public void StopResourceWithError(
            string key,
            string message,
            int? statusCode = null,
            string? stack = null,
            IReadOnlyDictionary<string, object?>? attributes = null)
        {
        }

        public void StopResourceWithError(
            string key,
            Exception exception,
            int? statusCode = null,
            IReadOnlyDictionary<string, object?>? attributes = null)
        {
        }

        public void AddTiming(string name)
        {
        }

        public void AddFeatureFlagEvaluation(string name, object value)
        {
        }

        public void AddAttribute(string key, object? value)
        {
        }

        public void AddAttributes(IReadOnlyDictionary<string, object?> attributes)
        {
        }

        public void RemoveAttribute(string key)
        {
        }

        public void RemoveAttributes(IEnumerable<string> keys)
        {
        }

        public void ReportAppFullyDisplayed()
        {
        }

        public void StopSession()
        {
        }

        public Task<string?> GetCurrentSessionIdAsync() =>
            new TaskCompletionSource<string?>().Task;
    }
}
