using FluentAssertions;

namespace LlmObservabilityLab.UseCases.Tests;

public sealed class ApplicationAssemblyTests
{
    [Fact]
    public void ShouldExposeExpectedAssemblyNameWhenApplicationIsBuilt()
    {
        string? assemblyName = typeof(Program).Assembly.GetName().Name;

        assemblyName.Should().Be("LlmObservabilityLab.Api");
    }
}
