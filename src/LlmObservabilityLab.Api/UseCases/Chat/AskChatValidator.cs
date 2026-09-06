using FluentValidation;
using LlmObservabilityLab.Api.Errors;

namespace LlmObservabilityLab.Api.UseCases.Chat;

public sealed class AskChatValidator : AbstractValidator<AskChatRequest>
{
    public AskChatValidator()
    {
        RuleFor(request => request.Prompt)
            .NotEmpty()
            .WithMessage(ErrorMessages.PromptRequired);
    }
}
