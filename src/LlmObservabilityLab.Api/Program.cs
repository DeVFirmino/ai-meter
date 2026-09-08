using LlmObservabilityLab.Api;
using LlmObservabilityLab.Api.Teams;
using Scalar.AspNetCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddApi(builder.Configuration, builder.Environment);

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
