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
        ChatResponse chatResponse = await _chatClient.GetResponseAsync(
            request.Prompt,
            cancellationToken: cancellationToken);

        return new AskChatResponse
        {
            Text = chatResponse.Text,
        };
    }
}
