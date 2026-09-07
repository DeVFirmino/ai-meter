using LlmObservabilityLab.Api.Errors;
using LlmObservabilityLab.Api.UseCases.Chat;
using LlmObservabilityLab.Api.Teams;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LlmObservabilityLab.Api.Controllers;

[ApiController]
[Route("api/chat")]
[EnableRateLimiting(TeamRateLimiting.PolicyName)]
public sealed class ChatController : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(AskChatResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Ask(
        [FromServices] IAskChatUseCase useCase,
        [FromServices] TeamContext teamContext,
        [FromBody] AskChatRequest request,
        CancellationToken cancellationToken)
    {
        AskChatResponse response = await useCase.Ask(request, teamContext.TeamId, cancellationToken);

        return Ok(response);
    }
}
