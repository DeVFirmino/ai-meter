using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LlmObservabilityLab.WebApi.Tests.TestUtilities;

public sealed class ChatApiFactory : WebApplicationFactory<Program>
{
    public TestChatClientBuilder ChatClient { get; } = new();

    public int RequestsPerMinute { get; init; } = 1000;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("AzureOpenAI:Endpoint", "https://unused.example.com/");
        builder.UseSetting("AzureOpenAI:DeploymentName", "test-model");
        builder.UseSetting("OTEL_SDK_DISABLED", "true");
        builder.UseSetting("Teams:RequestsPerMinute", RequestsPerMinute.ToString());
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IChatClient>();
            services.AddSingleton(ChatClient.Build());
        });
    }
}
