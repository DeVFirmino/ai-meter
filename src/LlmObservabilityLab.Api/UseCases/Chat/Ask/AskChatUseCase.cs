using FluentValidation.Results;
using LlmObservabilityLab.Api.Errors;
using Microsoft.Extensions.AI;
using LlmObservabilityLab.Api.Teams;
using LlmObservabilityLab.Api.Telemetry;

namespace LlmObservabilityLab.Api.UseCases.Chat.Ask;

public sealed class AskChatUseCase : IAskChatUseCase
{
    private readonly IChatClient _chatClient;
    private readonly AiUsageMeter _usageMeter;

    public AskChatUseCase(IChatClient chatClient, AiUsageMeter usageMeter)
    {
        _chatClient = chatClient;
        _usageMeter = usageMeter;
    }

    public async Task<AskChatResponse> Ask(
        AskChatRequest request,
        string teamId,
        CancellationToken cancellationToken)
    {
        Validate(request);

        ChatOptions options = new()
        {
            AdditionalProperties = new AdditionalPropertiesDictionary
            {
                [TeamContext.PropertyName] = teamId,
            },
        };

        ChatResponse chatResponse = await _chatClient.GetResponseAsync(
            request.Prompt,
            options,
            cancellationToken: cancellationToken);

        _usageMeter.RecordTokens(
            teamId,
            chatResponse.ModelId,
            chatResponse.Usage?.InputTokenCount ?? 0,
            chatResponse.Usage?.OutputTokenCount ?? 0);

        return new AskChatResponse
        {
            Text = chatResponse.Text,
        };
    }

    private static void Validate(AskChatRequest request)
    {
        var validator = new AskChatValidator();
        ValidationResult validationResult = validator.Validate(request);
        if (validationResult.IsValid is false)
        {
            throw new ValidationFailedException(
                [.. validationResult.Errors.Select(failure => failure.ErrorMessage)]);
        }
    }
}
