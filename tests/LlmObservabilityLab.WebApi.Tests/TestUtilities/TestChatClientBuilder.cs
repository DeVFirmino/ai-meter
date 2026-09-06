using System.Collections.Concurrent;
using Microsoft.Extensions.AI;
using Moq;

namespace LlmObservabilityLab.WebApi.Tests.TestUtilities;

public sealed class TestChatClientBuilder
{
    private readonly Mock<IChatClient> _chatClient = new();
    private readonly ConcurrentQueue<ChatOptions> _receivedOptions = new();

    public IReadOnlyList<ChatOptions> ReceivedOptions => _receivedOptions.ToArray();

    public IChatClient Build()
    {
        _chatClient.Setup(client => client.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .Returns(async (IEnumerable<ChatMessage> messages, ChatOptions? options,
                CancellationToken cancellationToken) =>
            {
                if (options is not null)
                {
                    _receivedOptions.Enqueue(options);
                }

                await Task.Yield();
                cancellationToken.ThrowIfCancellationRequested();

                string teamId = options?.AdditionalProperties?["team.id"] as string ?? "missing-team";
                return new ChatResponse(new ChatMessage(ChatRole.Assistant, teamId));
            });

        return _chatClient.Object;
    }

    public void VerifyNotCalled()
    {
        _chatClient.Verify(client => client.GetResponseAsync(
            It.IsAny<IEnumerable<ChatMessage>>(),
            It.IsAny<ChatOptions?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
