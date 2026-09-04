using LlmObservabilityLab.Api.UseCases.Chat;
using Microsoft.AspNetCore.Mvc;

namespace LlmObservabilityLab.Api.Controllers;

[ApiController]
[Route("api/chat")]
public sealed class ChatController : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(AskChatResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Ask(
        [FromServices] IAskChatUseCase useCase,
        [FromBody] AskChatRequest request,
        CancellationToken cancellationToken)
    {
        AskChatResponse response = await useCase.Ask(request, cancellationToken);

        return Ok(response);
    }
}
