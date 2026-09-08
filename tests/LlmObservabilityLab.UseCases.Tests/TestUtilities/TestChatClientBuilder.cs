using Microsoft.Extensions.AI;
using Moq;

namespace LlmObservabilityLab.UseCases.Tests.TestUtilities;

public sealed class TestChatClientBuilder
{
    private readonly Mock<IChatClient> _chatClient = new();

    public TestChatClientBuilder WithResponse(
        string responseText,
        long inputTokens = 0,
        long outputTokens = 0,
        string? modelId = null)
    {
        var response = new ChatResponse(
            new ChatMessage(ChatRole.Assistant, responseText))
        {
            ModelId = modelId,
            Usage = new UsageDetails
            {
                InputTokenCount = inputTokens,
                OutputTokenCount = outputTokens,
            },
        };

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

    public void VerifyNotCalled()
    {
        _chatClient.Verify(
            client => client.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    public void VerifyReceivedTeam(string teamId)
    {
        _chatClient.Verify(client => client.GetResponseAsync(
            It.IsAny<IEnumerable<ChatMessage>>(),
            It.Is<ChatOptions?>(options => HasTeam(options, teamId)),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static bool HasTeam(ChatOptions? options, string teamId)
    {
        return options?.AdditionalProperties is not null
            && options.AdditionalProperties.TryGetValue("team.id", out object? value)
            && value is string actualTeamId
            && actualTeamId == teamId;
    }
}
