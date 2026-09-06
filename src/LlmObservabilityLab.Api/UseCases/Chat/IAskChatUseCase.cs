namespace LlmObservabilityLab.Api.UseCases.Chat;

public interface IAskChatUseCase
{
    Task<AskChatResponse> Ask(
        AskChatRequest request,
        string teamId,
        CancellationToken cancellationToken);
}
