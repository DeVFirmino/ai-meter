using System.Net;

namespace LlmObservabilityLab.Api.Errors;

public sealed class ValidationFailedException : LlmObservabilityLabException
{
    public ValidationFailedException(IReadOnlyList<string> errors)
        : base(ErrorMessages.ValidationFailed) => Errors = errors;

    public override HttpStatusCode StatusCode => HttpStatusCode.BadRequest;

    public override IReadOnlyList<string> Errors { get; }
}
