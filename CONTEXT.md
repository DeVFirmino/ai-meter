# LlmObservabilityLab — contexto do projeto

> Coloque este arquivo na raiz do repositório como `CONTEXT.md` (ou renomeie para
> `CLAUDE.md` / `AGENTS.md` se quiser que o agente do Rider carregue automaticamente).

- **Jira (epic):** https://danielphellipe.atlassian.net/browse/DS-41
- **Revisão técnica completa:** https://claude.ai/code/artifact/b4430cfe-fd4d-45d0-8452-8babd0a971a9
- **Datado em:** 2026-09-03 — todas as APIs abaixo foram verificadas contra fonte oficial nessa data.

---

## 0. Estado atual do repositório (2026-09-04)

Scaffold-base concluído em .NET 10:

- API em `src/LlmObservabilityLab.Api/`, configurada com `AddControllers()` + `MapControllers()`
- template Minimal API `weatherforecast` removido
- projetos `LlmObservabilityLab.UseCases.Tests` e `LlmObservabilityLab.Traffic` registrados na solução
- SDK fixado, versões NuGet centralizadas e warnings tratados como erros
- guardrails locais e de CI instalados; `scripts/verify.sh` concentra os gates do repositório
- `Microsoft.AspNetCore.OpenApi` removido porque o scaffold original resolvia uma dependência transitiva
  com vulnerabilidade de severidade alta

Próximo passo: fase 0 da infraestrutura, mantendo a aplicação sem credenciais no repositório.

---

## 1. O que é

Laboratório prático de **observabilidade para aplicações que usam LLM**, do request HTTP até a
chamada ao modelo. O app é deliberadamente simples; **a telemetria é o assunto**.

Ciclo: construir → instrumentar → gerar tráfego → quebrar → analisar telemetria → escrever.

O entregável final é um artigo do blog danieldias.dev, sequência do post existente
*"OpenTelemetry in .NET: how traces, metrics and logs fit together"* (demo `country-search`).
Título de trabalho: **"What an HTTP 200 hides: observing LLM calls in .NET with OpenTelemetry and Azure Monitor"**.

Isso significa: **não reintroduzir OpenTelemetry do zero**. O leitor já sabe o que é span, métrica e log.
O assunto novo é o que muda quando a dependência é um modelo.

---

## 2. Convenções de código (não negociáveis)

Seguem o `dotnet-style` do Daniel:

- **Controllers MVC**, não Minimal API. Injeção de dependência no *action* com `[FromServices]`.
- **Use cases com `Execute`**, não handlers/MediatR. Um use case por operação.
- Legibilidade acima de concisão. Sem primary constructor em classe.
- Nomes de teste no padrão `Should...When...`.
- Sem abstração especulativa: o lab tem um único fluxo, não precisa de camada de domínio.

---

## 3. Stack verificada

| Item | Decisão | Por quê |
|---|---|---|
| SDK do modelo | **`OpenAI` 2.13** contra o endpoint **v1 do Azure OpenAI** | `Azure.AI.OpenAI` está parado na 2.1.0 estável desde dez/2024, só publica betas, e o próprio README pede para removê-lo |
| Endpoint | `https://<resource>.openai.azure.com/openai/v1/` | GA, **sem** `api-version` |
| Auth | `BearerTokenPolicy` + `DefaultAzureCredential`, escopo `https://ai.azure.com/.default` | `az login` local, Managed Identity no deploy. **Zero API key no repo.** |
| Abstração | `Microsoft.Extensions.AI` **10.9 (GA)** — `IChatClient` | A doc oficial do quickstart ainda diz `--prerelease`; está desatualizada |
| Telemetria | `UseOpenTelemetry()` do M.E.AI | ActivitySource e Meter chamam-se **`Experimental.Microsoft.Extensions.AI`** |
| Backend local | **Aspire Dashboard standalone** (tem GenAI visualizer desde 9.5) | Fase 1–2 inteira sem custo nem latência de ingestão |
| Backend Azure | `Azure.Monitor.OpenTelemetry.AspNetCore` 1.6 | Troca de exporter por configuração, não por reescrita |
| Modelo | `gpt-4.1-mini`, 1 deployment, **TPM baixo de propósito** | O rate limit é experimento, não acidente |

### Armadilhas já mapeadas

1. **O sampler padrão do Azure Monitor Distro mudou na 1.5.0 (abr/2026)**: era `ApplicationInsightsSampler`
   a 100%, agora é `RateLimitedSampler` a **5 traces/segundo**. A doc do Learn ainda está desatualizada.
   Isso *silenciosamente* destrói o experimento de volume — e por isso vira o melhor capítulo do artigo.
2. **Não ligar a instrumentação OTel do SDK `OpenAI` junto com a do M.E.AI** — spans e métricas duplicados.
3. **Retries já existem por padrão.** `ClientRetryPolicy`: até 3 tentativas, backoff exponencial a partir
   de 0,8s, **sem jitter**, em 408/429/5xx, honrando `Retry-After`. `NetworkTimeout` de 100s.
   Não escrever "retries, se houver" — eles estão lá.
4. **Semconv GenAI ainda é `Development`.** Nomes podem mudar sem major. `gen_ai.system` virou
   `gen_ai.provider.name` na 1.37. Fixar a versão do pacote e anotar a data no artigo.
5. **Custo não é métrica do app.** O app não sabe tipo de deployment, região nem desconto de cache.
   Custo estimado vive em **KQL**, com tabela de preços datada. Nunca chamar estimativa de "billing".

---

## 4. O que vem de graça vs. o que é manual

**Automático pelo `UseOpenTelemetry()` do M.E.AI:**

`gen_ai.provider.name`, `gen_ai.request.model`, `gen_ai.response.model`, `gen_ai.response.id`,
`gen_ai.response.finish_reasons`, `gen_ai.usage.input_tokens`, `gen_ai.usage.output_tokens`,
`gen_ai.usage.cache_read.input_tokens`, `gen_ai.usage.reasoning.output_tokens`,
`gen_ai.response.time_to_first_chunk`, `error.type`, `server.address`.

Métricas: `gen_ai.client.token.usage` (histograma), `gen_ai.client.operation.duration`,
`time_to_first_chunk`, `time_per_output_chunk`.

**Manual (é aqui que mora o trabalho do lab):**

- Tag de experimento (`lab.experiment`) e de intent (`lab.intent`) no span do modelo
- Contador de `finish_reason` como métrica própria
- Tokens estimados por tokenizer local, para comparar com o reportado
- Custo (em KQL, não no app)

---

## 5. Arquitetura

```
Traffic (console) ──► Api (controllers) ──► IChatClient pipeline ──► Azure OpenAI v1
                          └──► OpenTelemetry ──► Aspire Dashboard  (fases 1–2)
                                              └► Azure Monitor     (fase 3+)
```

Sem Redis, fila, banco, Kubernetes, Kafka ou microsserviços. Nada entra sem resolver um problema real do lab.

### Estrutura de pastas

```
LlmObservabilityLab/
├── src/LlmObservabilityLab.Api/
│   ├── Controllers/ChatController.cs              # POST /api/chat, POST /api/chat/stream
│   ├── UseCases/Chat/
│   │   ├── IAskChatUseCase.cs
│   │   ├── AskChatUseCase.cs                      # .Execute(request, ct)
│   │   ├── AskChatRequest.cs
│   │   └── AskChatResponse.cs
│   ├── Chat/
│   │   ├── ChatClientRegistration.cs              # OpenAIClient + pipeline M.E.AI
│   │   ├── ExperimentTagChatClient.cs             # DelegatingChatClient: tags lab.* no span gen_ai
│   │   ├── IntentCatalog.cs                       # explain | summarize | rewrite → system prompt
│   │   └── AzureOpenAIOptions.cs
│   ├── Telemetry/TelemetryRegistration.cs         # Aspire ou Azure Monitor por configuração; sampler explícito
│   └── Program.cs
├── tests/LlmObservabilityLab.UseCases.Tests/      # IChatClient fake, sem rede
├── tools/LlmObservabilityLab.Traffic/             # console: cenários A–J, header X-Lab-Experiment
├── infra/main.bicep  +  infra/apim/               # apim só na fase 7
├── queries/                                       # *.kql versionados
├── docs/experiments/                              # 1 md por experimento: hipótese, comando, resultado
├── docs/screenshots/
└── README.md
```

**Por que cada pasta:** `src` é o app instrumentado; `tools/Traffic` é um console .NET no próprio repo
(não k6) para versionar os cenários junto dos resultados; `queries` guarda o KQL porque a consulta *é*
parte do experimento; `docs/experiments` obriga a escrever a hipótese **antes** de rodar.

---

## 6. Registro do cliente (forma correta)

```csharp
#pragma warning disable OPENAI001 // BearerTokenPolicy ainda é marcada experimental no SDK

var credential = new DefaultAzureCredential();
var tokenPolicy = new BearerTokenPolicy(credential, "https://ai.azure.com/.default");

var openAiClient = new OpenAIClient(tokenPolicy, new OpenAIClientOptions
{
    Endpoint = new Uri($"https://{options.ResourceName}.openai.azure.com/openai/v1/"),
});

builder.Services
    .AddChatClient(openAiClient.GetChatClient(options.DeploymentName).AsIChatClient())
    .UseOpenTelemetry(configure: client => client.EnableSensitiveData = false)
    .Use(inner => new ExperimentTagChatClient(inner));

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource("Experimental.Microsoft.Extensions.AI"))
    .WithMetrics(metrics => metrics.AddMeter("Experimental.Microsoft.Extensions.AI"));
```

> **Ordem importa.** No `ChatClientBuilder`, **o primeiro `Use*` registrado é o mais externo**.
> `ExperimentTagChatClient` vem *depois* de `UseOpenTelemetry` justamente para rodar **dentro** do span
> `chat` e conseguir enriquecê-lo via `Activity.Current?.SetTag("lab.experiment", ...)`.

---

## 7. Experimentos

| # | Experimento | Hipótese |
|---|---|---|
| A | Baseline (3 tamanhos de input × 3 de output) | Duração é função de *output* tokens, quase indiferente a input tokens |
| B | Streaming vs. não-streaming | Duração total igual; TTFC é ~10% dela |
| C | **Truncamento silencioso** | HTTP 200, `success=true`, `finish_reason=length`. Nenhum alerta padrão pega |
| D | Rate limit real | 429 + `Retry-After`; p95 explode por causa do backoff, não do modelo. Repetir com `MaxRetries = 0` |
| E | **A cota é estimada, não medida** | O rate limiter reserva `max_tokens` na entrada; prompt curto com `max_tokens` alto toma 429 antes |
| F | Catálogo de falhas | 404, 400 content_filter, timeout, cancelamento — cada um com seu `error.type` |
| G | Sampler | Métricas mostram 300 chamadas, traces mostram ~5/s |
| H | Reconciliação | Soma do app vs. métricas da plataforma vs. Cost Management (chega em 8–72h → **rodar primeiro**) |
| I | Prompt cache *(opcional)* | `cache_read > 0` a partir da 2ª chamada com prefixo > 1024 tokens |
| J | Modelo com reasoning *(opcional)* | reasoning tokens >> texto visível |

---

## 8. KQL de partida (`queries/`)

```kql
// Latência e tokens da chamada ao modelo, por experimento
dependencies
| where name startswith "chat "
| extend experiment = tostring(customDimensions["lab.experiment"]),
         inTok  = toint(customDimensions["gen_ai.usage.input_tokens"]),
         outTok = toint(customDimensions["gen_ai.usage.output_tokens"])
| summarize calls = count(),
            p50 = percentile(duration, 50), p95 = percentile(duration, 95),
            inTok = sum(inTok), outTok = sum(outTok)
  by experiment
```

```kql
// Truncamento silencioso: HTTP 200 com finish_reason = length
dependencies
| where name startswith "chat "
| extend finish = tostring(customDimensions["gen_ai.response.finish_reasons"])
| summarize count() by finish, resultCode, success
```

---

## 9. Plano incremental (cada fase termina testável)

| Fase | O quê | Pronto quando |
|---|---|---|
| 0 | Infra mínima (Foundry + deployment + TPM baixo) | `curl` no endpoint v1 com token do `az account get-access-token` responde 200 |
| 1 | API + `IChatClient` + Aspire Dashboard local | Dashboard mostra `POST /api/chat → chat gpt-4.1-mini → POST …` com tokens |
| 2 | Enriquecimento + gerador de tráfego + streaming | Tag de experimento aparece em **cada** span do modelo |
| 3 | Azure Monitor | Rodar **primeiro com o sampler padrão**, registrar o estrago, depois corrigir. KQL retorna linhas por experimento |
| 4 | Experimentos A–H, um por vez, hipótese escrita antes | **Rodar H primeiro** (espera de faturamento); escrever por último |
| 5 | Deploy em Container Apps com user-assigned identity | Application Map liga a API ao Azure OpenAI e **não existe chave no repo** |
| 6 | Artigo 1 | Publicado |
| 7 | APIM (artigo 2) | Instância criada e apagada no mesmo fim de semana |

---

## 10. Segurança

- `EnableSensitiveData` **fixado explicitamente em código, por ambiente**. A env
  `OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT` liga a captura de prompts **sem tocar no código**.
- Não usar `UseLogging` em nível Trace.
- **Application Insights não redige nada**: quem tem Reader lê os prompts.
- Endpoint público sem auth é proxy aberto para LLM pago — restringir antes de qualquer deploy.
- Carga sintética apenas. Nenhum dado real, nenhum PII.
- Alerta de orçamento no Azure **antes** do primeiro teste de carga.

---

## 11. Não construir agora

Custo como métrica do app · function calling · RAG · histórico de conversa · cache semântico ·
Redis · Kubernetes · Kafka · microsserviços · autenticação de usuários.

---

## 12. Pontos não verificados (confirmar durante a construção)

- [ ] Azure OpenAI honra `traceparent` de entrada?
- [ ] Trace único ponta a ponta através do APIM (não há garantia escrita na doc)
- [ ] Opção exata para ajustar o rate-limited sampler na distro 1.6
- [ ] Papel RBAC exigido pelo escopo `ai.azure.com`
- [ ] Nome instável: `gen_ai.usage.cache_write.input_tokens` vs. `cache_creation.input_tokens`

---

## 13. Fase 7 — APIM (artigo separado, não agora)

O gateway vê o que o app não vê: tokens de **todos** os consumidores por subscription/produto,
enforcement (429 por TPM, 403 por cota), log central de prompts (`ApiManagementGatewayLlmLog`),
backend pools e circuit breaker. O app vê o que o gateway não vê: contexto de negócio, retries
internos do SDK, latência percebida pelo usuário.

Políticas atuais: `llm-token-limit`, `llm-emit-token-metric`, `llm-semantic-cache-*`, `llm-content-safety`
(os nomes `azure-openai-*` redirecionam). **Consumption não suporta `llm-token-limit` nem backend pools** —
precisa Developer (~US$ 48/mês, provisiona em 30–40 min) ou Basic v2 (~US$ 150/mês, minutos).
Cobrança por hora: criar, rodar, apagar.
