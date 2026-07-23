using DatadogNet;
using Xunit;

namespace DatadogNet.UnitTests;

/// <summary>
/// Covers the rendering that <c>DatadogNet 3.14.0.1</c> got wrong on iOS.
/// </summary>
/// <remarks>
/// The bug was not subtle once seen — the same span reported
/// <c>6096355397431041644</c> on iOS and <c>6a61e4ff000000002e430f579ece9a6c</c> on Android — but it
/// survived twenty green device runs, because the only thing asserted about a trace id was that it
/// was not empty. These are the assertions that would have caught it.
/// </remarks>
public class TraceIdentifiersTests
{
    /// <summary>The high half as Datadog writes it: 32 bits of unix seconds, then 32 zeros.</summary>
    private const string HighOrderBits = "6a61e4ff00000000";

    [Fact]
    public void Reassembles_the_two_halves_into_the_32_character_form()
    {
        // 3333525018185210476 == 0x2e430f579ece9a6c, the low half of the id observed on Android.
        var traceId = TraceIdentifiers.ToHexTraceId(
            "3333525018185210476",
            $"_dd.p.tid={HighOrderBits}");

        Assert.Equal("6a61e4ff000000002e430f579ece9a6c", traceId);
    }

    [Fact]
    public void Pads_the_high_half_with_zeros_when_128_bit_ids_are_off()
    {
        // No _dd.p.tid is the DD64bTraceId case, and dd-sdk-android still renders it 32 wide -
        // toHexString() is toHexStringPadded(id, 32) there too. A 16-character answer would not
        // match what the backend was given for the same trace.
        var traceId = TraceIdentifiers.ToHexTraceId("3333525018185210476", datadogTags: null);

        Assert.Equal("00000000000000002e430f579ece9a6c", traceId);
        Assert.Equal(32, traceId.Length);
    }

    [Theory]
    [InlineData("_dd.p.dm=-1,_dd.p.tid=6a61e4ff00000000")]
    [InlineData("_dd.p.tid=6a61e4ff00000000,_dd.p.dm=-1")]
    [InlineData("_dd.p.dm=-1,_dd.p.tid=6a61e4ff00000000,_dd.p.usr=abc")]
    [InlineData(" _dd.p.tid = 6a61e4ff00000000 ")]
    public void Finds_the_high_half_wherever_it_sits_among_the_other_tags(string tags)
    {
        Assert.Equal(
            "6a61e4ff000000002e430f579ece9a6c",
            TraceIdentifiers.ToHexTraceId("3333525018185210476", tags));
    }

    [Theory]
    [InlineData("_dd.p.tid=")]
    [InlineData("_dd.p.tid=zzzzzzzzzzzzzzzz")]
    [InlineData("_dd.p.tid=6a61e4ff")]              // too short to be a 64-bit half
    [InlineData("_dd.p.tid=6a61e4ff000000000")]     // too long
    [InlineData("_dd.p.tidy=6a61e4ff00000000")]     // a different tag with a shared prefix
    [InlineData("garbage")]
    [InlineData("")]
    public void Falls_back_to_a_zero_high_half_rather_than_guessing(string tags)
    {
        // A half-parsed high half would name a real-looking trace that nothing ever reported, which
        // is worse than admitting to only having the low 64 bits.
        Assert.Equal(
            "00000000000000002e430f579ece9a6c",
            TraceIdentifiers.ToHexTraceId("3333525018185210476", tags));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-number")]
    [InlineData("0x2e430f579ece9a6c")]   // the header is decimal; hex here means something is wrong
    [InlineData("-1")]
    [InlineData("99999999999999999999999")]
    public void Reports_no_trace_id_at_all_when_the_header_is_not_a_number(string? lowOrderBits)
    {
        // Empty rather than a plausible-looking zero: DatadogHttpMessageHandler puts this on a RUM
        // resource as _dd.trace_id, and an id that parses but names nothing is a correlation that
        // silently points at the wrong place.
        Assert.Equal(string.Empty, TraceIdentifiers.ToHexTraceId(lowOrderBits, null));
    }

    [Fact]
    public void Handles_the_boundary_values_without_truncating()
    {
        Assert.Equal(
            "0000000000000000ffffffffffffffff",
            TraceIdentifiers.ToHexTraceId("18446744073709551615", null));

        Assert.Equal(
            "ffffffffffffffffffffffffffffffff",
            TraceIdentifiers.ToHexTraceId("18446744073709551615", "_dd.p.tid=ffffffffffffffff"));

        Assert.Equal(
            "00000000000000000000000000000000",
            TraceIdentifiers.ToHexTraceId("0", null));
    }

    [Fact]
    public void Renders_in_lower_case_whatever_case_the_tag_arrived_in()
    {
        // Hex digits are compared as strings downstream, so case is not cosmetic.
        Assert.Equal(
            "6a61e4ff000000002e430f579ece9a6c",
            TraceIdentifiers.ToHexTraceId("3333525018185210476", "_dd.p.tid=6A61E4FF00000000"));
    }
}
