using DatadogNet;
using Xunit;

namespace DatadogNet.UnitTests;

/// <summary>
/// Covers which hosts are handed your trace ids.
/// </summary>
/// <remarks>
/// Every other test in this project protects a dashboard. This one protects against sending
/// <c>x-datadog-trace-id</c> — and with it the shape of your internal topology — to a stranger who
/// registered a domain ending in yours. It is worth being pedantic about.
/// </remarks>
public class FirstPartyHostTests
{
    private static readonly string[] Hosts = ["example.com", "api.internal"];

    [Theory]
    [InlineData("example.com")]
    [InlineData("api.example.com")]
    [InlineData("a.b.c.example.com")]
    [InlineData("api.internal")]
    [InlineData("staging.api.internal")]
    public void Matches_the_host_itself_and_any_subdomain(string host) =>
        Assert.True(DatadogHttpMessageHandler.IsFirstParty(host, Hosts));

    [Theory]
    [InlineData("notexample.com")]
    [InlineData("myexample.com")]
    [InlineData("example.com.evil.net")]
    [InlineData("example.como")]
    [InlineData("xexample.com")]
    [InlineData("api-internal")]
    [InlineData("internal")]
    [InlineData("com")]
    [InlineData("")]
    public void Does_not_match_a_host_that_merely_ends_in_one(string host)
    {
        // "notexample.com" is the case a plain EndsWith gets wrong, and it is trivially registrable
        // by anyone who reads your app's traffic. The boundary check is the whole point.
        Assert.False(DatadogHttpMessageHandler.IsFirstParty(host, Hosts));
    }

    [Theory]
    [InlineData("EXAMPLE.COM")]
    [InlineData("Api.Example.Com")]
    public void Matches_regardless_of_case(string host)
    {
        // Hosts are case-insensitive, and a configuration written in the case someone's designer
        // used should not quietly stop propagating.
        Assert.True(DatadogHttpMessageHandler.IsFirstParty(host, Hosts));
    }

    [Fact]
    public void Matches_a_configured_host_written_in_a_different_case()
    {
        Assert.True(DatadogHttpMessageHandler.IsFirstParty("api.example.com", ["EXAMPLE.COM"]));
    }

    [Fact]
    public void Ignores_empty_entries_rather_than_matching_everything()
    {
        // An empty candidate is a configuration mistake - a trailing comma in a list, most likely.
        // Suffix-matching on it would make every host in the world first-party, which is the worst
        // possible reading of a typo.
        Assert.False(DatadogHttpMessageHandler.IsFirstParty("anything.at.all", ["", "example.com"]));
        Assert.True(DatadogHttpMessageHandler.IsFirstParty("api.example.com", ["", "example.com"]));
    }

    [Fact]
    public void Matches_nothing_when_nothing_is_configured() =>
        Assert.False(DatadogHttpMessageHandler.IsFirstParty("example.com", []));

    [Fact]
    public void Rejects_null_arguments_rather_than_treating_them_as_no_match()
    {
        Assert.Throws<ArgumentNullException>(
            () => DatadogHttpMessageHandler.IsFirstParty(null!, Hosts));

        Assert.Throws<ArgumentNullException>(
            () => DatadogHttpMessageHandler.IsFirstParty("example.com", null!));
    }
}
