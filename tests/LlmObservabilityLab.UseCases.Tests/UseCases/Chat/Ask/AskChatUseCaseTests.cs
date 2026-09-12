using System.Net;
using FluentAssertions;
using LlmObservabilityLab.Api.Errors;
using LlmObservabilityLab.Api.UseCases.Chat.Ask;
using LlmObservabilityLab.UseCases.Tests.TestUtilities;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;

namespace LlmObservabilityLab.UseCases.Tests.UseCases.Chat.Ask;

public sealed class AskChatUseCaseTests
{
    [Theory]
    [InlineData("engineering")]
    [InlineData("support")]
    public async Task ShouldReturnAssistantTextWhenPromptIsValid(string teamId)
    {
        const string prompt = "Why does LLM observability matter?";
        const string assistantText = "It reveals what an HTTP 200 hides.";
        using var cancellationTokenSource = new CancellationTokenSource();
        CancellationToken cancellationToken = cancellationTokenSource.Token;
        TestChatClientBuilder chatClientBuilder = new TestChatClientBuilder()
            .WithResponse(assistantText);
        using IChatClient chatClient = chatClientBuilder.Build();
        using TokenMetricProbe probe = new();
        var useCase = new AskChatUseCase(chatClient, probe.Meter);
        var request = new AskChatRequest
        {
            Prompt = prompt,
        };

        AskChatResponse response = await useCase.Execute(request, teamId, cancellationToken);

        response.Text.Should().Be(assistantText);
        chatClientBuilder.VerifyReceivedPrompt(prompt, cancellationToken);
        chatClientBuilder.VerifyReceivedTeam(teamId);
    }

    [Fact]
    public async Task ShouldThrowValidationFailedExceptionWhenPromptIsEmpty()
    {
        TestChatClientBuilder chatClientBuilder = new TestChatClientBuilder();
        using IChatClient chatClient = chatClientBuilder.Build();
        using TokenMetricProbe probe = new();
        var useCase = new AskChatUseCase(chatClient, probe.Meter);
        var request = new AskChatRequest
        {
            Prompt = string.Empty,
        };

        Func<Task> act = () => useCase.Execute(request, "engineering", CancellationToken.None);

        ValidationFailedException exception = (await act.Should()
            .ThrowAsync<ValidationFailedException>()).Which;
        exception.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        exception.Errors.Should().Equal(ErrorMessages.PromptRequired);
        chatClientBuilder.VerifyNotCalled();
        probe.Collector.GetMeasurementSnapshot().Should().BeEmpty();
    }

    [Fact]
    public async Task ShouldRecordTokensWithTeamAndModelWhenClientReportsUsage()
    {
        TestChatClientBuilder chatClientBuilder = new TestChatClientBuilder()
            .WithResponse("ok", inputTokens: 12, outputTokens: 40, modelId: "gpt-4.1-mini");
        using IChatClient chatClient = chatClientBuilder.Build();
        using TokenMetricProbe probe = new();
        var useCase = new AskChatUseCase(chatClient, probe.Meter);
        var request = new AskChatRequest
        {
            Prompt = "Explain observability.",
        };

        await useCase.Execute(request, "support", CancellationToken.None);

        IReadOnlyList<CollectedMeasurement<long>> measurements = probe.Collector.GetMeasurementSnapshot();
        measurements.Should().HaveCount(2);
        CollectedMeasurement<long> input = measurements.Single(measurement => (string?)measurement.Tags["token.type"] == "input");
        CollectedMeasurement<long> output = measurements.Single(measurement => (string?)measurement.Tags["token.type"] == "output");
        input.Value.Should().Be(12);
        output.Value.Should().Be(40);
        foreach (CollectedMeasurement<long> measurement in measurements)
        {
            measurement.Tags["team.id"].Should().Be("support");
            measurement.Tags["gen_ai.response.model"].Should().Be("gpt-4.1-mini");
        }
    }
}
