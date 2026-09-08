namespace LlmObservabilityLab.Api.UseCases.Chat.Ask;

public sealed record AskChatResponse
{
    public required string Text { get; init; }
}
