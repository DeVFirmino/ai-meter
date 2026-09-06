using System.Net;

namespace LlmObservabilityLab.Api.Errors;

public abstract class LlmObservabilityLabException : Exception
{
    protected LlmObservabilityLabException(string message)
        : base(message)
    {
    }

    public abstract HttpStatusCode StatusCode { get; }

    public abstract IReadOnlyList<string> Errors { get; }
}
