namespace LlmObservabilityLab.Api.UseCases.Chat;

public sealed record AskChatRequest
{
    public string Prompt { get; init; } = string.Empty;
}
