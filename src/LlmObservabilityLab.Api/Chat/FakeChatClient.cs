using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace LlmObservabilityLab.Api.Chat;

/// <summary>
/// Development-only stand-in for Azure OpenAI. Answers instantly with invented
/// token counts (long-context sized, so estimated cost reads in dollars) and latency
/// so the AI Meter dashboard can be filled without cost.
/// </summary>
public sealed class FakeChatClient : IChatClient
{
    private const string ModelId = "gpt-4.1-mini-fake";

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        await Task.Delay(Random.Shared.Next(20, 150), cancellationToken);

        return new ChatResponse(new ChatMessage(ChatRole.Assistant, "Fake answer for the AI Meter lab."))
        {
            ModelId = ModelId,
            FinishReason = ChatFinishReason.Stop,
            Usage = new UsageDetails
            {
                InputTokenCount = Random.Shared.Next(80_000, 600_000),
                OutputTokenCount = Random.Shared.Next(8_000, 80_000),
            },
        };
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ChatResponse response = await GetResponseAsync(messages, options, cancellationToken);
        yield return new ChatResponseUpdate(ChatRole.Assistant, response.Text);
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        return serviceType.IsInstanceOfType(this) ? this : null;
    }

    public void Dispose()
    {
    }
}
