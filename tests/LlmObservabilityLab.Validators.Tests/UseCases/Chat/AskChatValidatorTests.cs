using FluentAssertions;
using FluentValidation.TestHelper;
using LlmObservabilityLab.Api.Errors;
using LlmObservabilityLab.Api.UseCases.Chat;

namespace LlmObservabilityLab.Validators.Tests.UseCases.Chat;

public sealed class AskChatValidatorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    [InlineData("\n")]
    public void ShouldFailWhenPromptIsMissing(string? prompt)
    {
        AskChatValidator validator = new AskChatValidator();
        var request = new AskChatRequest
        {
            Prompt = prompt!,
        };

        TestValidationResult<AskChatRequest> result = validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(current => current.Prompt)
            .WithErrorMessage(ErrorMessages.PromptRequired);
    }

    [Fact]
    public void ShouldSucceedWhenPromptHasContent()
    {
        AskChatValidator validator = new AskChatValidator();
        var request = new AskChatRequest
        {
            Prompt = "Why does LLM observability matter?",
        };

        TestValidationResult<AskChatRequest> result = validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }
}
