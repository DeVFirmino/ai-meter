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

    public async Task<AskChatResponse> Execute(
        AskChatRequest request,
        string teamId,
        CancellationToken cancellationToken)
    {
        Validate(request);

        // The OpenTelemetry wrapper copies these onto the gen_ai span only when
        // EnableSensitiveData is on, which ChatClientRegistration ties to Development.
        // Team attribution outside Development comes from TeamContextMiddleware, which
        // tags the request span and the HTTP metric, and from AiUsageMeter.
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
