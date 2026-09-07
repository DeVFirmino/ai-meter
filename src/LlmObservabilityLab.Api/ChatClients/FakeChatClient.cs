using Microsoft.Extensions.AI;

namespace LlmObservabilityLab.Api.ChatClients;

/// <summary>
/// Development-only stand-in for Azure OpenAI: answers instantly with invented
/// long-context token counts so the AI Meter dashboard can be filled at no cost.
/// </summary>
public sealed class FakeChatClient : IChatClient
{
    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        await Task.Delay(Random.Shared.Next(20, 150), cancellationToken);

        return new ChatResponse(new ChatMessage(ChatRole.Assistant, "Fake answer for the AI Meter lab."))
        {
            ModelId = "gpt-4.1-mini-fake",
            Usage = new UsageDetails
            {
                InputTokenCount = Random.Shared.Next(80_000, 600_000),
                OutputTokenCount = Random.Shared.Next(8_000, 80_000),
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

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}
