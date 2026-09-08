using LlmObservabilityLab.Api.ChatClients;
using LlmObservabilityLab.Api.Errors;
using LlmObservabilityLab.Api.Filters;
using LlmObservabilityLab.Api.Teams;
using LlmObservabilityLab.Api.Telemetry;
using LlmObservabilityLab.Api.UseCases.Chat.Ask;
using Microsoft.AspNetCore.Mvc;

namespace LlmObservabilityLab.Api;

public static class DependencyInjectionExtension
{
    public static IServiceCollection AddApi(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddControllers(options =>
        {
            options.Filters.Add<ExceptionFilter>();
            options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
        })
        .ConfigureApiBehaviorOptions(options =>
        {
            options.InvalidModelStateResponseFactory = CreateValidationErrorResponse;
        });
        services.AddOpenApi();

        services.AddScoped<IAskChatUseCase, AskChatUseCase>();
        services.AddScoped<TeamContext>();
        services.AddScoped<TeamContextMiddleware>();
        services.AddSingleton<AiUsageMeter>();

        services.AddModelChatClient(configuration, environment);
        services.AddTeamRateLimiting(configuration);
        services.AddTelemetry(environment.ApplicationName);

        return services;
    }

    private static IActionResult CreateValidationErrorResponse(ActionContext context)
    {
        return new BadRequestObjectResult(new ErrorResponse
        {
            Errors = [ErrorMessages.ValidationFailed],
        });
    }
}
