using Azure.AI.OpenAI;
using Azure.Identity;
using LlmObservabilityLab.Api.UseCases.Chat;
using Microsoft.Extensions.AI;
using Scalar.AspNetCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

string azureOpenAiEndpoint = builder.Configuration["AzureOpenAI:Endpoint"]
    ?? throw new InvalidOperationException("AzureOpenAI:Endpoint is required.");
string azureOpenAiDeploymentName = builder.Configuration["AzureOpenAI:DeploymentName"]
    ?? throw new InvalidOperationException("AzureOpenAI:DeploymentName is required.");

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddScoped<IAskChatUseCase, AskChatUseCase>();
builder.Services.AddSingleton<IChatClient>(_ =>
    new AzureOpenAIClient(
            new Uri(azureOpenAiEndpoint),
            new DefaultAzureCredential())
        .GetChatClient(azureOpenAiDeploymentName)
        .AsIChatClient());

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();

public partial class Program;
