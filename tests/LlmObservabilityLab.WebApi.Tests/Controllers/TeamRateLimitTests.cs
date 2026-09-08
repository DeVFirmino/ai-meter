using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using LlmObservabilityLab.Api.Errors;
using LlmObservabilityLab.WebApi.Tests.TestUtilities;

namespace LlmObservabilityLab.WebApi.Tests.Controllers;

public sealed class TeamRateLimitTests
{
    [Fact]
    public async Task ShouldRejectWith429WhenTeamExceedsItsRequestsPerMinute()
    {
        await using ChatApiFactory factory = new() { RequestsPerMinute = 2 };
        using HttpClient client = factory.CreateChatClient();

        HttpStatusCode first = await AskAsync(client, "engineering", CancellationToken.None);
        HttpStatusCode second = await AskAsync(client, "engineering", CancellationToken.None);
        using HttpRequestMessage request = new ChatRequestBuilder("engineering").Build();
        using HttpResponseMessage third = await client.SendAsync(request);

        first.Should().Be(HttpStatusCode.OK);
        second.Should().Be(HttpStatusCode.OK);
        third.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        third.Headers.RetryAfter.Should().NotBeNull();
        ErrorResponse? body = await third.Content.ReadFromJsonAsync<ErrorResponse>();
        body.Should().BeOfType<ErrorResponse>().Subject.Errors.Should().Equal(ErrorMessages.TeamRateLimited);
        factory.ChatClient.ReceivedOptions.Should().HaveCount(2);
    }

    [Fact]
    public async Task ShouldNotConsumeTheOtherTeamsQuotaWhenOneTeamIsLimited()
    {
        await using ChatApiFactory factory = new() { RequestsPerMinute = 1 };
        using HttpClient client = factory.CreateChatClient();

        HttpStatusCode engineeringFirst = await AskAsync(client, "engineering", CancellationToken.None);
        HttpStatusCode engineeringSecond = await AskAsync(client, "engineering", CancellationToken.None);
        HttpStatusCode support = await AskAsync(client, "support", CancellationToken.None);

        engineeringFirst.Should().Be(HttpStatusCode.OK);
        engineeringSecond.Should().Be(HttpStatusCode.TooManyRequests);
        support.Should().Be(HttpStatusCode.OK);
        factory.ChatClient.ReceivedOptions.Should().HaveCount(2);
    }

    [Fact]
    public async Task ShouldRejectBeforeRateLimitingWhenTeamIsInvalid()
    {
        await using ChatApiFactory factory = new() { RequestsPerMinute = 1 };
        using HttpClient client = factory.CreateChatClient();

        HttpStatusCode status = await AskAsync(client, "marketing", CancellationToken.None);

        status.Should().Be(HttpStatusCode.BadRequest);
        factory.ChatClient.VerifyNotCalled();
    }

    private static async Task<HttpStatusCode> AskAsync(
        HttpClient client,
        string teamId,
        CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = new ChatRequestBuilder(teamId).Build();
        using HttpResponseMessage response = await client.SendAsync(request, cancellationToken);
        return response.StatusCode;
    }
}
