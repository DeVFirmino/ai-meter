using FluentAssertions;
using LlmObservabilityLab.Api.ChatClients;
using Microsoft.Extensions.AI;

namespace LlmObservabilityLab.WebApi.Tests.ChatClients;

public sealed class FakeChatClientTests
{
    [Fact]
    public async Task ShouldReturnFixedUsageWhenPromptsDiffer()
    {
        using var client = new FakeChatClient();

        ChatResponse first = await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "Explain tokens.")],
            cancellationToken: CancellationToken.None);
        ChatResponse second = await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "Summarise this much longer prompt about observability.")],
            cancellationToken: CancellationToken.None);

        foreach (ChatResponse response in new[] { first, second })
        {
            response.Text.Should().Be("Fake answer for the AI Meter lab.");
            response.ModelId.Should().Be("gpt-4.1-mini-fake");
            response.Usage.Should().BeOfType<UsageDetails>().Subject.InputTokenCount.Should().Be(100_000);
            response.Usage.Should().BeOfType<UsageDetails>().Subject.OutputTokenCount.Should().Be(10_000);
        }
    }

    [Fact]
    public async Task ShouldCancelWhenCancellationIsRequested()
    {
        using var client = new FakeChatClient();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Func<Task> act = () => client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "Explain tokens.")],
            cancellationToken: cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
