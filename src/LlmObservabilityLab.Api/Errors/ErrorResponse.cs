namespace LlmObservabilityLab.Api.Errors;

public sealed record ErrorResponse
{
    public required IReadOnlyList<string> Errors { get; init; }

    public string? CorrelationId { get; init; }
}
