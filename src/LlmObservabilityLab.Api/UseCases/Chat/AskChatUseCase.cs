using FluentValidation.Results;
using LlmObservabilityLab.Api.Errors;
using Microsoft.Extensions.AI;

namespace LlmObservabilityLab.Api.UseCases.Chat;

public sealed class AskChatUseCase : IAskChatUseCase
{
    private readonly IChatClient _chatClient;

    public AskChatUseCase(IChatClient chatClient)
    {
        _chatClient = chatClient;
    }

    public async Task<AskChatResponse> Ask(
        AskChatRequest request,
        CancellationToken cancellationToken)
    {
        Validate(request);

        ChatResponse chatResponse = await _chatClient.GetResponseAsync(
            request.Prompt,
            cancellationToken: cancellationToken);

        return new AskChatResponse
        {
            Text = chatResponse.Text,
        };
    }

    private static void Validate(AskChatRequest request)
    {
        AskChatValidator validator = new AskChatValidator();
        ValidationResult validationResult = validator.Validate(request);
        if (validationResult.IsValid is false)
        {
            throw new ValidationFailedException(
                [.. validationResult.Errors.Select(failure => failure.ErrorMessage)]);
        }
    }
}
