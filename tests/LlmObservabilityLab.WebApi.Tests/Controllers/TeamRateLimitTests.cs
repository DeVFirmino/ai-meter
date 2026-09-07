using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using LlmObservabilityLab.Api.Errors;
using LlmObservabilityLab.Api.UseCases.Chat;
using LlmObservabilityLab.WebApi.Tests.TestUtilities;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LlmObservabilityLab.WebApi.Tests.Controllers;

public sealed class TeamRateLimitTests
{
    [Fact]
    public async Task ShouldRejectWith429WhenTeamExceedsItsRequestsPerMinute()
    {
        await using ChatApiFactory factory = new() { RequestsPerMinute = 2 };
        using HttpClient client = CreateClient(factory);

        HttpStatusCode first = await Ask(client, "engineering");
        HttpStatusCode second = await Ask(client, "engineering");
        using HttpResponseMessage third = await client.SendAsync(CreateRequest("engineering"));

        first.Should().Be(HttpStatusCode.OK);
        second.Should().Be(HttpStatusCode.OK);
        third.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        third.Headers.RetryAfter.Should().NotBeNull();
        ErrorResponse? body = await third.Content.ReadFromJsonAsync<ErrorResponse>();
        body!.Errors.Should().Equal(ErrorMessages.TeamRateLimited);
        factory.ChatClient.ReceivedOptions.Should().HaveCount(2);
    }

    [Fact]
    public async Task ShouldNotConsumeTheOtherTeamsQuotaWhenOneTeamIsLimited()
    {
        await using ChatApiFactory factory = new() { RequestsPerMinute = 1 };
        using HttpClient client = CreateClient(factory);

        HttpStatusCode engineeringFirst = await Ask(client, "engineering");
        HttpStatusCode engineeringSecond = await Ask(client, "engineering");
        HttpStatusCode support = await Ask(client, "support");

        engineeringFirst.Should().Be(HttpStatusCode.OK);
        engineeringSecond.Should().Be(HttpStatusCode.TooManyRequests);
        support.Should().Be(HttpStatusCode.OK);
        factory.ChatClient.ReceivedOptions.Should().HaveCount(2);
    }

    [Fact]
    public async Task ShouldStillRejectInvalidTeamBeforeRateLimiting()
    {
        await using ChatApiFactory factory = new() { RequestsPerMinute = 1 };
        using HttpClient client = CreateClient(factory);

        HttpStatusCode status = await Ask(client, "marketing");

        status.Should().Be(HttpStatusCode.BadRequest);
        factory.ChatClient.VerifyNotCalled();
    }

    private static async Task<HttpStatusCode> Ask(HttpClient client, string teamId)
    {
        using HttpResponseMessage response = await client.SendAsync(CreateRequest(teamId));
        return response.StatusCode;
    }

    private static HttpClient CreateClient(ChatApiFactory factory)
    {
        return factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });
    }

    private static HttpRequestMessage CreateRequest(string teamId)
    {
        HttpRequestMessage request = new(HttpMethod.Post, "/api/chat")
        {
            Content = JsonContent.Create(new AskChatRequest { Prompt = "Explain observability." }),
        };
        request.Headers.TryAddWithoutValidation("X-Team-Id", teamId);
        return request;
    }
}
