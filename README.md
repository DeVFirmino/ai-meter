# AI Meter

AI Meter is an ASP.NET Core lab for tracking LLM token usage and estimated cost per team with OpenTelemetry and Grafana. Tokens are grouped by model, the cost estimate uses the reference prices for gpt-4.1-mini, and each team has its own request rate limit.

Run the lab with a fake model first, then follow the request through team identification, token recording and the limiter. The same flow can call your Azure OpenAI deployment.

Read the accompanying blog post: [AI Meter: run a lab for LLM usage, estimated cost and team limits](https://danieldias.dev/en/blog/ai-meter-usage-and-cost-per-team).

![Grafana showing simulated token usage and estimated cost for engineering and support, with three engineering calls blocked by the request limit](docs/img/ai-meter-grafana-demo.png)

The screenshot uses the fake model: token counts are invented and the displayed cost represents no actual spending.

## What you get

With two teams sending traffic, the Grafana dashboard shows:

- Estimated cost per team, from tokens times the model's reference price.
- Accumulated tokens per team, split by input and output and by model.
- Tokens per minute by team and type.
- Rejected calls (4xx and 5xx) per team, with a separate panel for calls blocked by the quota (429).
- p95 latency of `/api/chat` per team.

## Run the lab

You need the .NET SDK selected by [global.json](global.json) and Docker. Run the commands below from the repository root.

### 1. Start Grafana and the API

```bash
docker run -d --name lgtm -p 3000:3000 -p 4317:4317 grafana/otel-lgtm
AzureOpenAI__UseFakeClient=true Teams__RequestsPerMinute=2 \
  dotnet run --project src/LlmObservabilityLab.Api --launch-profile http
```

If the `lgtm` container already exists, use `docker start lgtm`. The `http` profile selects Development, listens on `http://localhost:5286` and exports OTLP to `http://localhost:4317`.

The fake client returns `Fake answer for the AI Meter lab.` after a short simulated delay. Its model ID is `gpt-4.1-mini-fake`, and its token counts are invented. It makes no Azure calls and is available only in Development.

Open [Grafana](http://localhost:3000), sign in with `admin` / `admin`, go to Dashboards, New, Import, and upload [grafana/dashboards/ai-meter.json](grafana/dashboards/ai-meter.json).

### 2. Prove that each team has its own quota

In another terminal, send three requests within one minute:

```bash
for i in 1 2 3; do
  curl -s -o /dev/null -X POST http://localhost:5286/api/chat \
    -H 'Content-Type: application/json' -H 'X-Team-Id: engineering' \
    -d '{"prompt":"What is a token?"}' -w '%{http_code}\n'
done
```

With a freshly started API, expect `200`, `200`, then `429`. Now send a request from support:

```bash
curl -i -X POST http://localhost:5286/api/chat \
  -H 'Content-Type: application/json' -H 'X-Team-Id: support' \
  -d '{"prompt":"What is a token?"}'
```

Expect HTTP 200 and `{"text":"Fake answer for the AI Meter lab."}`. Engineering has used its allowance, but support has its own window. Another engineering request within that window returns 429 with `Retry-After` and an `errors` array. Restart the API before repeating the whole sequence to clear both allowances.

After the next metrics export, normally about a minute later, Grafana should show tokens for both teams and one blocked engineering call. The p95 panel and tokens-per-minute panel need multiple samples, so keep sending traffic over several minutes to inspect them.

### 3. Connect a real model

Stop the fake API. With Azure CLI installed, run `az login` using an identity that has the `Cognitive Services OpenAI User` role on your resource. Then supply the resource endpoint and deployment name:

```bash
AzureOpenAI__UseFakeClient=false \
  AzureOpenAI__Endpoint='https://<resource>.openai.azure.com/' \
  AzureOpenAI__DeploymentName=gpt-4.1-mini \
  dotnet run --project src/LlmObservabilityLab.Api --launch-profile http
```

The client uses `Azure.AI.OpenAI` and `DefaultAzureCredential`. The SDK handles the service API path, so configure the resource endpoint shown above. Managed Identity is excluded in Development and remains available in other environments. Real calls consume your Azure quota.

| Setting | Meaning |
|---|---|
| `AzureOpenAI:UseFakeClient` | Select the simulated client in Development; defaults to false. |
| `AzureOpenAI:Endpoint` | Resource endpoint, required when the real client is resolved. |
| `AzureOpenAI:DeploymentName` | Azure deployment name, required for the real client. |
| `Teams:RequestsPerMinute` | Requests per team in a 60-second fixed window; defaults to 60. |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | OTLP destination; local launch profiles set `http://localhost:4317`. |

Use double underscores for nested environment settings, as in `Teams__RequestsPerMinute`. Prompt and response capture is explicitly enabled in Development and disabled elsewhere in [ChatClientRegistration](src/LlmObservabilityLab.Api/ChatClients/ChatClientRegistration.cs). Use synthetic prompts in the lab.

## How it fits together

![AI Meter architecture](docs/img/ai-meter-architecture.png)

The caller sends `POST /api/chat` with an `X-Team-Id` header. A middleware validates the team and tags the trace, the log scope and the ASP.NET request metric with it. The rate limiter keeps one window per team and answers 429 above the quota. The use case calls the model through `IChatClient` and records the returned token usage in a counter tagged with team and model. OpenTelemetry exports everything over OTLP to the `grafana/otel-lgtm` container, and the dashboard in `grafana/dashboards/ai-meter.json` reads Prometheus.

## Identify the team on every call

`TeamContextMiddleware` reads the header once, rejects unknown teams before the controller runs, and copies the team to traces, logs and request metrics:

```csharp
if (TryReadTeam(context.Request, out string teamId) is false)
{
    await RejectAsync(context);
    return;
}

_teamContext.TeamId = teamId;
Activity.Current?.SetTag(TeamContext.PropertyName, teamId);
context.Features.Get<IHttpMetricsTagsFeature>()?.Tags.Add(
    new KeyValuePair<string, object?>(TeamContext.PropertyName, teamId));
```

`TeamContext` is a scoped object, so the controller and the rate limiter read the same team for the rest of the request. The `IHttpMetricsTagsFeature` line is what makes latency and failures per team possible without a second histogram: ASP.NET already measures every request, and this adds `team.id` to that measurement.

In this lab the team is a header value. In your API it has to come from the caller's identity, otherwise anyone can pick another team's quota by editing a header.

## Count tokens per team and model

`AiUsageMeter` owns one counter, `ai_meter.tokens`, created through `IMeterFactory`. After each model call, the use case records what the model reported:

```csharp
_usageMeter.RecordTokens(
    teamId,
    chatResponse.ModelId,
    chatResponse.Usage?.InputTokenCount ?? 0,
    chatResponse.Usage?.OutputTokenCount ?? 0);
```

Each measurement carries three tags: `team.id`, `token.type` (`input` or `output`) and `gen_ai.request.model`. The model tag matters for cost, because prices differ per model and per direction. `ModelId` comes from the response, so it carries the exact model version the provider served.

## Export to Grafana

`TelemetryRegistration` enables ASP.NET Core and HttpClient instrumentation, listens to the `Experimental.Microsoft.Extensions.AI` source and meter, adds the `AiMeter` meter, and sends traces, metrics and logs through one OTLP exporter. In Prometheus the counter appears as `ai_meter_tokens_total` with labels `team_id`, `token_type` and `gen_ai_request_model`.

## Turn tokens into money

The dashboard calculates cost in PromQL using its configured reference prices for gpt-4.1-mini: 0.40 USD per million input tokens and 1.60 USD per million output tokens, labelled September 2026 in the JSON:

```promql
sum by (team_id) (last_over_time(ai_meter_tokens_total{token_type="input"}[$__range])) * 0.40 / 1e6
+ sum by (team_id) (last_over_time(ai_meter_tokens_total{token_type="output"}[$__range])) * 1.60 / 1e6
```

Prices can change in the query without an API deploy. The result estimates token cost and does not reconcile the Azure invoice. The cumulative panels use `last_over_time`, which reads the latest sample in the selected range, so they mean "since the API started". `increase()` measures growth between samples, and a short run whose first sample already contains all the calls shows zero. For continuous traffic, use `increase()` to measure consumption over an interval; the tokens-per-minute panel uses `rate()`.

The cost query applies those rates to all recorded models, including the fake model. If you change models or mix them, update the query to filter and price each model before using the estimate.

## Cap each team

ASP.NET Core's built-in rate limiter partitions requests by the resolved team:

```csharp
options.AddPolicy(PolicyName, httpContext =>
{
    string teamId = httpContext.RequestServices.GetRequiredService<TeamContext>().TeamId;

    return RateLimitPartition.GetFixedWindowLimiter(teamId, _ => new FixedWindowRateLimiterOptions
    {
        PermitLimit = requestsPerMinute,
        Window = TimeSpan.FromMinutes(1),
        QueueLimit = 0,
    });
});
```

`ChatController` opts in with `[EnableRateLimiting("per-team")]`, so other endpoints stay unlimited. The quota comes from `Teams:RequestsPerMinute` (60 by default). A rejected call gets 429, a `Retry-After` header and the same error body the API uses everywhere. Because the request metric already carries the team, the 429s show up per team in Grafana with no extra code.

## Code conventions

The API is one executable project with folders for the responsibilities it already has. [Program.cs](src/LlmObservabilityLab.Api/Program.cs) shows the HTTP pipeline in order: routing, team resolution, rate limiting and controllers. [DependencyInjectionExtension](src/LlmObservabilityLab.Api/DependencyInjectionExtension.cs) registers the application through `AddApi`, while the client and telemetry registrations own their respective configuration.

| Concern | Convention in this repository |
|---|---|
| HTTP | MVC controllers, literal routes, explicit binding attributes and dependencies injected into each action with `[FromServices]`. Actions return `IActionResult` and declare response types. |
| Operations | One folder per operation under `UseCases/<Area>/<Operation>`, containing the interface, use case, validator and request/response records. |
| Use case methods | One public method with the operation's verb: `IAskChatUseCase.Ask`. This project explicitly overrides the house convention of `Execute`. |
| Async work | Required `CancellationToken` as the last parameter, passed to I/O. Other async methods use `Async`; controller actions, test names and framework signatures follow their own contracts. |
| Validation | FluentValidation runs at the start of the use case. Request records use named properties with defaults; response records use `required` named properties. |
| Errors | `ErrorResponse` contains an `errors` array and optional `correlationId`. MVC binding failures, team rejection and quota rejection use that shape. `ExceptionFilter` maps application exceptions and hides unexpected exception details. |
| C# | File-scoped namespaces matching folders, sealed concrete classes, explicit constructors and readonly dependency fields. Types stay visible at declaration sites; nullable warnings and build warnings are errors. |
| Tests | `Should...When...` names, FluentAssertions and Moq behind builders. Use case, validator and HTTP tests have separate projects. |

The controller passes the team as a string to `Ask`; the use case has no `HttpContext` dependency. Validation stays in the operation so callers outside MVC receive the same rules. Avoid adding a mediator, generic repository or extra application layers until a concrete responsibility needs them.

Malformed JSON and bodies that cannot bind return HTTP 400 with `ErrorMessages.ValidationFailed`. An omitted, null or blank prompt reaches `AskChatValidator` and returns HTTP 400 with `ErrorMessages.PromptRequired`. Both responses use the same `errors` array.

## Verify a change

```bash
bash scripts/verify.sh
```

The script restores packages, builds, runs all tests, checks formatting and reports vulnerable direct and transitive packages. The SDK is selected in `global.json`, package versions live in `Directory.Packages.props`, and `.editorconfig` supplies the code style settings used by build and format checks.

For a test-only run, use `dotnet test`. The tests replace the model client and make no Azure calls. They cover prompt validation, the error contract, token tags, concurrent team requests and independent quotas. Web API tests use `WebApplicationFactory<Program>` to exercise the real MVC pipeline.

## Where this example stops

- The team is a header. Production needs it from authentication.
- The quota counts requests. Azure bills tokens, and a request limiter decides before the model reports usage, so a token budget needs a limiter of its own.
- The quota is held in memory per API instance. Restarting clears it, and multiple instances each have their own allowance.
- Prices live in the dashboard JSON. Configuration with a date is easier to audit.
- The backend is a local container. Azure Monitor is the next stop for the same signals.

## Troubleshooting

After the Mac sleeps, the Docker VM's clock lags and Prometheus stamps samples hours in the past, so the dashboard looks empty. Widen the time range to find them, and check `docker exec lgtm date` before a demo.

Grafana's metrics explorer does not list metrics that arrive over OTLP, because they carry no metadata. Type the metric name in Code mode.

The first real call can spend seconds on local authentication. `DefaultAzureCredential` probes Managed Identity on a laptop, so the API excludes that credential in Development.
