using FluentAssertions;
using LlmObservabilityLab.Api.Errors;
using LlmObservabilityLab.Api.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;

namespace LlmObservabilityLab.WebApi.Tests.Filters;

public sealed class ExceptionFilterTests
{
    [Fact]
    public void ShouldReturnBadRequestErrorResponseWhenValidationFailed()
    {
        ExceptionFilter filter = new ExceptionFilter(NullLogger<ExceptionFilter>.Instance);
        DefaultHttpContext httpContext = new();
        httpContext.Request.Path = "/api/chat";
        ExceptionContext exceptionContext = CreateExceptionContext(
            httpContext,
            new ValidationFailedException([ErrorMessages.PromptRequired]));

        filter.OnException(exceptionContext);

        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        ObjectResult result = exceptionContext.Result.Should().BeOfType<ObjectResult>().Subject;
        ErrorResponse response = result.Value.Should().BeOfType<ErrorResponse>().Subject;
        response.Errors.Should().Equal(ErrorMessages.PromptRequired);
        response.CorrelationId.Should().BeNull();
    }

    [Fact]
    public void ShouldReturnInternalErrorResponseWhenExceptionIsUnexpected()
    {
        const string correlationId = "chat-trace-1";
        ExceptionFilter filter = new ExceptionFilter(NullLogger<ExceptionFilter>.Instance);
        DefaultHttpContext httpContext = new();
        httpContext.Request.Path = "/api/chat";
        httpContext.TraceIdentifier = correlationId;
        ExceptionContext exceptionContext = CreateExceptionContext(
            httpContext,
            new InvalidOperationException("The model client failed."));

        filter.OnException(exceptionContext);

        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        ObjectResult result = exceptionContext.Result.Should().BeOfType<ObjectResult>().Subject;
        ErrorResponse response = result.Value.Should().BeOfType<ErrorResponse>().Subject;
        response.Errors.Should().Equal(ErrorMessages.UnexpectedError);
        response.CorrelationId.Should().Be(correlationId);
    }

    private static ExceptionContext CreateExceptionContext(
        HttpContext httpContext,
        Exception exception)
    {
        ActionContext actionContext = new(
            httpContext,
            new RouteData(),
            new ActionDescriptor());

        return new ExceptionContext(actionContext, [])
        {
            Exception = exception,
        };
    }
}
