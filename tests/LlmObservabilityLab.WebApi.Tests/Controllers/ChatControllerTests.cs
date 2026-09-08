using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using LlmObservabilityLab.Api.Errors;
using LlmObservabilityLab.Api.UseCases.Chat.Ask;
using LlmObservabilityLab.WebApi.Tests.TestUtilities;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.AI;

namespace LlmObservabilityLab.WebApi.Tests.Controllers;

public sealed class ChatControllerTests
{
    [Theory]
    [InlineData("engineering")]
    [InlineData("support")]
    public async Task ShouldPassTeamToChatClientWhenHeaderIsValid(string teamId)
    {
        await using ChatApiFactory factory = new();
        using HttpClient client = CreateClient(factory);
        using HttpRequestMessage request = CreateRequest(teamId);

        using HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        AskChatResponse? body = await response.Content.ReadFromJsonAsync<AskChatResponse>();
        body.Should().BeOfType<AskChatResponse>().Subject.Text.Should().Be(teamId);
        factory.ChatClient.ReceivedOptions.Should().ContainSingle();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("unknown")]
    [InlineData("Engineering")]
    [InlineData("engineering,support")]
    public async Task ShouldRejectRequestWithoutCallingModelWhenTeamIsInvalid(string? teamId)
    {
        await using ChatApiFactory factory = new();
        using HttpClient client = CreateClient(factory);
        using HttpRequestMessage request = CreateRequest(teamId);

        using HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ErrorResponse? body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        body.Should().BeOfType<ErrorResponse>().Subject.Errors.Should().Equal(ErrorMessages.TeamInvalid);
        factory.ChatClient.VerifyNotCalled();
    }

    [Fact]
    public async Task ShouldRejectRequestWhenTeamHeaderHasMultipleValues()
    {
        await using ChatApiFactory factory = new();
        using HttpClient client = CreateClient(factory);
        using HttpRequestMessage request = CreateRequest("engineering");
        request.Headers.TryAddWithoutValidation("X-Team-Id", "support");

        using HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        factory.ChatClient.VerifyNotCalled();
    }

    [Fact]
    public async Task ShouldKeepOptionsSeparateWhenTeamsSendConcurrentRequests()
    {
        await using ChatApiFactory factory = new();
        using HttpClient client = CreateClient(factory);
        using HttpRequestMessage engineeringRequest = CreateRequest("engineering");
        using HttpRequestMessage supportRequest = CreateRequest("support");

        Task<HttpResponseMessage> engineeringTask = client.SendAsync(engineeringRequest);
        Task<HttpResponseMessage> supportTask = client.SendAsync(supportRequest);
        await Task.WhenAll(engineeringTask, supportTask);
        using HttpResponseMessage engineeringResponse = await engineeringTask;
        using HttpResponseMessage supportResponse = await supportTask;

        engineeringResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        supportResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        AskChatResponse? engineering = await engineeringResponse.Content.ReadFromJsonAsync<AskChatResponse>();
        AskChatResponse? support = await supportResponse.Content.ReadFromJsonAsync<AskChatResponse>();
        engineering.Should().BeOfType<AskChatResponse>().Subject.Text.Should().Be("engineering");
        support.Should().BeOfType<AskChatResponse>().Subject.Text.Should().Be("support");
        IReadOnlyList<ChatOptions> options = factory.ChatClient.ReceivedOptions;
        options.Should().HaveCount(2);
        options[0].Should().NotBeSameAs(options[1]);
        options[0].AdditionalProperties.Should().NotBeSameAs(options[1].AdditionalProperties);
    }

    [Fact]
    public async Task ShouldReturnPromptValidationErrorWhenTeamIsValidAndPromptIsEmpty()
    {
        await using ChatApiFactory factory = new();
        using HttpClient client = CreateClient(factory);
        using HttpRequestMessage request = CreateRequest("engineering", string.Empty);

        using HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ErrorResponse? body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        body.Should().BeOfType<ErrorResponse>().Subject.Errors.Should().Equal(ErrorMessages.PromptRequired);
        factory.ChatClient.VerifyNotCalled();
    }

    [Fact]
    public async Task ShouldServeOpenApiWhenTeamHeaderIsAbsent()
    {
        await using ChatApiFactory factory = new();
        using HttpClient client = CreateClient(factory);

        using HttpResponseMessage response = await client.GetAsync("/openapi/v1.json");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        factory.ChatClient.VerifyNotCalled();
    }

    [Theory]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("{")]
    [InlineData("{\"prompt\":42}")]
    public async Task ShouldReturnErrorResponseWhenRequestBodyCannotBeBound(string json)
    {
        await using ChatApiFactory factory = new();
        using HttpClient client = CreateClient(factory);
        using HttpRequestMessage request = CreateRequest("engineering");
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        using HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errors").ValueKind.Should().Be(JsonValueKind.Array);
        body.GetProperty("errors").EnumerateArray()
            .Select(error => error.GetString()).Should().Equal(ErrorMessages.ValidationFailed);
        factory.ChatClient.VerifyNotCalled();
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"prompt\":null}")]
    public async Task ShouldReturnPromptValidationErrorWhenPromptIsMissing(string json)
    {
        await using ChatApiFactory factory = new();
        using HttpClient client = CreateClient(factory);
        using HttpRequestMessage request = CreateRequest("engineering");
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        using HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errors").ValueKind.Should().Be(JsonValueKind.Array);
        body.GetProperty("errors").EnumerateArray()
            .Select(error => error.GetString()).Should().Equal(ErrorMessages.PromptRequired);
        factory.ChatClient.VerifyNotCalled();
    }

    private static HttpClient CreateClient(ChatApiFactory factory)
    {
        return factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });
    }

    private static HttpRequestMessage CreateRequest(string? teamId, string prompt = "Explain observability.")
    {
        HttpRequestMessage request = new(HttpMethod.Post, "/api/chat")
        {
            Content = JsonContent.Create(new AskChatRequest { Prompt = prompt }),
        };

        if (teamId is not null)
        {
            request.Headers.TryAddWithoutValidation("X-Team-Id", teamId);
        }

        return request;
    }
}
