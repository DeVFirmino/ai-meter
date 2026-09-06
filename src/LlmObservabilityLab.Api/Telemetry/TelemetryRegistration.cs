using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace LlmObservabilityLab.Api.Telemetry;

public static class TelemetryRegistration
{
    private const string ChatClientTelemetrySourceName = "Experimental.Microsoft.Extensions.AI";

    public static IServiceCollection AddTelemetry(this IServiceCollection services, string serviceName)
    {
        services
            .AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddSource(ChatClientTelemetrySourceName))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddMeter(ChatClientTelemetrySourceName)
                .AddMeter(AiUsageMeter.MeterName))
            .WithLogging(
                configureBuilder: null,
                configureOptions: options =>
                {
                    options.IncludeScopes = true;
                    options.IncludeFormattedMessage = true;
                })
            .UseOtlpExporter();

        return services;
    }
}
