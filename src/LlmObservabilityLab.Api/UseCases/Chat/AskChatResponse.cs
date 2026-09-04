namespace LlmObservabilityLab.Api.UseCases.Chat;

public sealed record AskChatResponse
{
    public required string Text { get; init; }
}
