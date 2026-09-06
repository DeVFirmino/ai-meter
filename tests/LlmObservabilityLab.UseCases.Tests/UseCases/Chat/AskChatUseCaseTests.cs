using System.Net;
using FluentAssertions;
using LlmObservabilityLab.Api.Errors;
using LlmObservabilityLab.Api.UseCases.Chat;
using LlmObservabilityLab.UseCases.Tests.TestUtilities;
using Microsoft.Extensions.AI;

namespace LlmObservabilityLab.UseCases.Tests.UseCases.Chat;

public sealed class AskChatUseCaseTests
{
    [Theory]
    [InlineData("engineering")]
    [InlineData("support")]
    public async Task ShouldReturnAssistantTextWhenPromptIsValid(string teamId)
    {
        const string prompt = "Why does LLM observability matter?";
        const string assistantText = "It reveals what an HTTP 200 hides.";
        using var cancellationTokenSource = new CancellationTokenSource();
        CancellationToken cancellationToken = cancellationTokenSource.Token;
        TestChatClientBuilder chatClientBuilder = new TestChatClientBuilder()
            .WithResponse(assistantText);
        using IChatClient chatClient = chatClientBuilder.Build();
        var useCase = new AskChatUseCase(chatClient);
        var request = new AskChatRequest
        {
            Prompt = prompt,
        };

        AskChatResponse response = await useCase.Ask(request, teamId, cancellationToken);

        response.Text.Should().Be(assistantText);
        chatClientBuilder.VerifyReceivedPrompt(prompt, cancellationToken);
        chatClientBuilder.VerifyReceivedTeam(teamId);
    }

    [Fact]
    public async Task ShouldThrowValidationFailedExceptionWhenPromptIsEmpty()
    {
        TestChatClientBuilder chatClientBuilder = new TestChatClientBuilder();
        using IChatClient chatClient = chatClientBuilder.Build();
        var useCase = new AskChatUseCase(chatClient);
        var request = new AskChatRequest
        {
            Prompt = string.Empty,
        };

        Func<Task> act = () => useCase.Ask(request, "engineering", CancellationToken.None);

        ValidationFailedException exception = (await act.Should()
            .ThrowAsync<ValidationFailedException>()).Which;
        exception.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        exception.Errors.Should().Equal(ErrorMessages.PromptRequired);
        chatClientBuilder.VerifyNotCalled();
    }
}
