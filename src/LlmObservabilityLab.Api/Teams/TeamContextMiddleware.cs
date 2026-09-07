using System.Diagnostics;
using LlmObservabilityLab.Api.Errors;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Primitives;

namespace LlmObservabilityLab.Api.Teams;

public sealed class TeamContextMiddleware : IMiddleware
{
    private readonly TeamContext _teamContext;
    private readonly ILogger<TeamContextMiddleware> _logger;

    public TeamContextMiddleware(
        TeamContext teamContext,
        ILogger<TeamContextMiddleware> logger)
    {
        _teamContext = teamContext;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (TargetsController(context) is false)
        {
            await next(context);
            return;
        }

        if (TryReadTeam(context.Request, out string teamId) is false)
        {
            await RejectAsync(context);
            return;
        }

        _teamContext.TeamId = teamId;
        Activity.Current?.SetTag(TeamContext.PropertyName, teamId);
        context.Features.Get<IHttpMetricsTagsFeature>()?.Tags.Add(
            new KeyValuePair<string, object?>(TeamContext.PropertyName, teamId));

        using IDisposable? scope = _logger.BeginScope(new Dictionary<string, object>
        {
            [TeamContext.PropertyName] = teamId,
        });

        await next(context);
    }

    private static bool TargetsController(HttpContext context)
    {
        return context.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>() is not null;
    }

    private static bool TryReadTeam(HttpRequest request, out string teamId)
    {
        StringValues values = request.Headers[TeamContext.HeaderName];
        string? candidate = values.Count == 1 ? values[0] : null;

        if (candidate is not null && TeamContext.KnownTeams.Contains(candidate))
        {
            teamId = candidate;
            return true;
        }

        teamId = string.Empty;
        return false;
    }

    private static Task RejectAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return context.Response.WriteAsJsonAsync(
            new ErrorResponse { Errors = [ErrorMessages.TeamInvalid] },
            cancellationToken: context.RequestAborted);
    }
}
