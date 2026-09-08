using System.Net.Http.Json;
using System.Text;
using LlmObservabilityLab.Api.UseCases.Chat.Ask;

namespace LlmObservabilityLab.WebApi.Tests.TestUtilities;

public sealed class ChatRequestBuilder
{
    private readonly string? _teamId;
    private string _prompt = "Explain observability.";
    private string? _json;

    public ChatRequestBuilder(string? teamId)
    {
        _teamId = teamId;
    }

    public ChatRequestBuilder WithPrompt(string prompt)
    {
        _prompt = prompt;
        return this;
    }

    public ChatRequestBuilder WithJson(string json)
    {
        _json = json;
        return this;
    }

    public HttpRequestMessage Build()
    {
        HttpRequestMessage request = new(HttpMethod.Post, "/api/chat")
        {
            Content = _json is not null
                ? new StringContent(_json, Encoding.UTF8, "application/json")
                : JsonContent.Create(new AskChatRequest { Prompt = _prompt }),
        };

        if (_teamId is not null)
        {
            request.Headers.TryAddWithoutValidation("X-Team-Id", _teamId);
        }

        return request;
    }
}
