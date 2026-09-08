namespace LlmObservabilityLab.Api.UseCases.Chat.Ask;

public interface IAskChatUseCase
{
    Task<AskChatResponse> Ask(
        AskChatRequest request,
        string teamId,
        CancellationToken cancellationToken);
}

