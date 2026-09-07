using Azure.AI.OpenAI;
using Azure.Identity;
using LlmObservabilityLab.Api.ChatClients;
using LlmObservabilityLab.Api.Filters;
using LlmObservabilityLab.Api.Teams;
using LlmObservabilityLab.Api.Telemetry;
using LlmObservabilityLab.Api.UseCases.Chat;
using Microsoft.Extensions.AI;
using Scalar.AspNetCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

string azureOpenAiEndpoint = builder.Configuration["AzureOpenAI:Endpoint"]
    ?? throw new InvalidOperationException("AzureOpenAI:Endpoint is required.");
string azureOpenAiDeploymentName = builder.Configuration["AzureOpenAI:DeploymentName"]
    ?? throw new InvalidOperationException("AzureOpenAI:DeploymentName is required.");

builder.Services.AddControllers(options => options.Filters.Add<ExceptionFilter>());
builder.Services.AddOpenApi();
builder.Services.AddScoped<IAskChatUseCase, AskChatUseCase>();
builder.Services.AddScoped<TeamContext>();
builder.Services.AddScoped<TeamContextMiddleware>();
builder.Services.AddSingleton<AiUsageMeter>();
builder.Services.AddTeamRateLimiting(builder.Configuration);
builder.Services.AddTelemetry(builder.Environment.ApplicationName);

bool captureSensitiveData = builder.Environment.IsDevelopment();
DefaultAzureCredentialOptions credentialOptions = new()
{
    ExcludeManagedIdentityCredential = builder.Environment.IsDevelopment()
};

bool useFakeChatClient = builder.Environment.IsDevelopment()
    && builder.Configuration.GetValue<bool>("AzureOpenAI:UseFakeClient");

builder.Services
    .AddChatClient(_ => useFakeChatClient
        ? new FakeChatClient()
        : new AzureOpenAIClient(
                new Uri(azureOpenAiEndpoint),
                new DefaultAzureCredential(credentialOptions))
            .GetChatClient(azureOpenAiDeploymentName)
            .AsIChatClient())
    .UseOpenTelemetry(configure: client => client.EnableSensitiveData = captureSensitiveData);

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseMiddleware<TeamContextMiddleware>();
app.UseRateLimiter();
app.MapControllers();

app.Run();

public partial class Program;
