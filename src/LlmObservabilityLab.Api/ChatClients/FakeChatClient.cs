using Microsoft.Extensions.AI;

namespace LlmObservabilityLab.Api.ChatClients;

/// <summary>
/// Development-only stand-in for Azure OpenAI with fixed simulated delay and
/// token counts so the AI Meter demo has repeatable totals without Azure calls.
/// </summary>
public sealed class FakeChatClient : IChatClient
{
    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);

        return new ChatResponse(new ChatMessage(ChatRole.Assistant, "Fake answer for the AI Meter lab."))
        {
            ModelId = "gpt-4.1-mini-fake",
            Usage = new UsageDetails
            {
                InputTokenCount = 100_000,
                OutputTokenCount = 10_000,
            },
        };
    }

    // The three members below are required by IChatClient; the lab only uses GetResponseAsync.
    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("The fake client does not stream.");
    }

    // OpenTelemetryChatClient reads ChatClientMetadata through here to fill
    // gen_ai.provider.name and the model tags on the span. Returning null left them empty.
    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceKey is not null ? null
        : serviceType == typeof(ChatClientMetadata) ? new ChatClientMetadata("fake", null, "gpt-4.1-mini-fake")
        : serviceType == typeof(FakeChatClient) ? this
        : null;

    public void Dispose()
    {
    }
}
