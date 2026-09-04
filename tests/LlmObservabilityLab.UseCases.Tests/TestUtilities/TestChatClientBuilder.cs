using Microsoft.Extensions.AI;
using Moq;

namespace LlmObservabilityLab.UseCases.Tests.TestUtilities;

public sealed class TestChatClientBuilder
{
    private readonly Mock<IChatClient> _chatClient = new();

    public TestChatClientBuilder WithResponse(string responseText)
    {
        var response = new ChatResponse(
            new ChatMessage(ChatRole.Assistant, responseText));

        _chatClient
            .Setup(client => client.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        return this;
    }

    public IChatClient Build() => _chatClient.Object;

    public void VerifyReceivedPrompt(
        string expectedPrompt,
        CancellationToken expectedCancellationToken)
    {
        _chatClient.Verify(client => client.GetResponseAsync(
            It.Is<IEnumerable<ChatMessage>>(messages =>
                messages.Single().Text == expectedPrompt),
            It.IsAny<ChatOptions?>(),
            expectedCancellationToken),
            Times.Once);
    }
}
