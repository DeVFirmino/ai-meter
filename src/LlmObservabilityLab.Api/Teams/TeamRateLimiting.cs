using System.Threading.RateLimiting;
using LlmObservabilityLab.Api.Errors;
using Microsoft.AspNetCore.RateLimiting;

namespace LlmObservabilityLab.Api.Teams;

public static class TeamRateLimiting
{
    public const string PolicyName = "per-team";
    public const string RequestsPerMinuteKey = "Teams:RequestsPerMinute";

    public static IServiceCollection AddTeamRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        int requestsPerMinute = configuration.GetValue(RequestsPerMinuteKey, 60);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = WriteRejection;
            options.AddPolicy(PolicyName, httpContext =>
            {
                string teamId = httpContext.RequestServices.GetRequiredService<TeamContext>().TeamId;

                return RateLimitPartition.GetFixedWindowLimiter(teamId, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = requestsPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                });
            });
        });

        return services;
    }

    private static ValueTask WriteRejection(OnRejectedContext context, CancellationToken cancellationToken)
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
        }

        return new ValueTask(context.HttpContext.Response.WriteAsJsonAsync(
            new ErrorResponse { Errors = [ErrorMessages.TeamRateLimited] },
            cancellationToken));
    }
}
