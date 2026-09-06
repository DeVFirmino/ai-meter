using LlmObservabilityLab.Api.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace LlmObservabilityLab.Api.Filters;

public sealed class ExceptionFilter : IExceptionFilter
{
    private readonly ILogger<ExceptionFilter> _logger;

    public ExceptionFilter(ILogger<ExceptionFilter> logger)
    {
        _logger = logger;
    }

    public void OnException(ExceptionContext context)
    {
        if (context.Exception is LlmObservabilityLabException known)
        {
            _logger.LogWarning(known, "Handled failure on {Path}", context.HttpContext.Request.Path);

            context.HttpContext.Response.StatusCode = (int)known.StatusCode;
            context.Result = new ObjectResult(new ErrorResponse { Errors = known.Errors });

            return;
        }

        string correlationId = context.HttpContext.TraceIdentifier;

        _logger.LogError(
            context.Exception,
            "Unhandled failure {CorrelationId} on {Path}",
            correlationId,
            context.HttpContext.Request.Path);

        context.HttpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Result = new ObjectResult(new ErrorResponse
        {
            Errors = [ErrorMessages.UnexpectedError],
            CorrelationId = correlationId,
        });
    }
}
