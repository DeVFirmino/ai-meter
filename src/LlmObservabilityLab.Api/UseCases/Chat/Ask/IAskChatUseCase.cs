namespace LlmObservabilityLab.Api.UseCases.Chat.Ask;

public interface IAskChatUseCase
{
    Task<AskChatResponse> Execute(
        AskChatRequest request,
        string teamId,
        CancellationToken cancellationToken);
}

