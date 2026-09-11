using System.Diagnostics.Metrics;
using LlmObservabilityLab.Api.Teams;

namespace LlmObservabilityLab.Api.Telemetry;

public sealed class AiUsageMeter
{
    public const string MeterName = "AiMeter";

    private readonly Counter<long> _tokens;

    public AiUsageMeter(IMeterFactory meterFactory)
    {
        Meter meter = meterFactory.Create(MeterName);
        _tokens = meter.CreateCounter<long>(
            "ai_meter.tokens",
            unit: "{token}",
            description: "Tokens consumed per team, split by input and output.");
    }

    // The model comes from ChatResponse.ModelId, so it is the model that served the
    // call, not the one requested. Microsoft.Extensions.AI already exports
    // gen_ai.client.token.usage, but without a team attribute, which is why this
    // counter exists.
    public void RecordTokens(string teamId, string? model, long inputTokens, long outputTokens)
    {
        _tokens.Add(inputTokens,
            new KeyValuePair<string, object?>(TeamContext.PropertyName, teamId),
            new KeyValuePair<string, object?>("token.type", "input"),
            new KeyValuePair<string, object?>("gen_ai.response.model", model));

        _tokens.Add(outputTokens,
            new KeyValuePair<string, object?>(TeamContext.PropertyName, teamId),
            new KeyValuePair<string, object?>("token.type", "output"),
            new KeyValuePair<string, object?>("gen_ai.response.model", model));
    }
}
