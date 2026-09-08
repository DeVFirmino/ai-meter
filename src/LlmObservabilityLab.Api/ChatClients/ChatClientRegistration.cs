using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.AI;

namespace LlmObservabilityLab.Api.ChatClients;

public static class ChatClientRegistration
{
    public static IServiceCollection AddModelChatClient(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        bool isDevelopment = environment.IsDevelopment();
        bool useFakeClient = isDevelopment && configuration.GetValue<bool>("AzureOpenAI:UseFakeClient");

        services
            .AddChatClient(_ => useFakeClient
                ? new FakeChatClient()
                : CreateAzureOpenAIChatClient(configuration, isDevelopment))
            .UseOpenTelemetry(configure: client => client.EnableSensitiveData = isDevelopment);

        return services;
    }

    private static IChatClient CreateAzureOpenAIChatClient(
        IConfiguration configuration,
        bool isDevelopment)
    {
        string endpoint = configuration["AzureOpenAI:Endpoint"]
            ?? throw new InvalidOperationException("AzureOpenAI:Endpoint is required.");
        string deploymentName = configuration["AzureOpenAI:DeploymentName"]
            ?? throw new InvalidOperationException("AzureOpenAI:DeploymentName is required.");
        DefaultAzureCredentialOptions credentialOptions = new()
        {
            ExcludeManagedIdentityCredential = isDevelopment,
        };

        return new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential(credentialOptions))
            .GetChatClient(deploymentName)
            .AsIChatClient();
    }
}
