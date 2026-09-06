namespace LlmObservabilityLab.Api.Errors;

public static class ErrorMessages
{
    public const string PromptRequired = "The prompt is required.";
    public const string TeamInvalid = "Provide exactly one X-Team-Id header with engineering or support.";
    public const string ValidationFailed = "One or more validation errors occurred.";
    public const string UnexpectedError = "An unexpected error occurred.";
}
