# Infrastructure

Phase 0 is being created manually in the Azure portal so every resource and setting can be inspected
before it exists. Infrastructure as code is intentionally deferred until after the manual walkthrough.
API Management remains isolated in `apim/` until phase 7.

## Safety model

The infrastructure workflow has two different kinds of guardrail:

- Automated code gates live in `scripts/verify.sh` and do not access or modify Azure.
- Azure changes use the portal and stop at an explicit review checkpoint before creation.

An Azure budget sends alerts; it does not stop resources or enforce a hard spending cap. Cost data can
arrive later than the usage that produced it, so the budget cannot be treated as a real-time circuit
breaker.

## Phase 0 checkpoint log

State verified on 2026-09-04:

- [x] Active subscription selected.
- [x] `Microsoft.CognitiveServices` resource provider registered.
- [x] Resource group `rg-llm-observability-lab` created in Sweden Central.
- [x] Resource-group budget `budget-llm-observability-lab` created.
- [x] Budget amount set to EUR 5, resetting monthly.
- [x] Actual-cost alerts configured at 50%, 80%, and 100%.
- [x] Budget expiration set to 2027-05-04.
- [x] Azure OpenAI account created in Sweden Central with SKU `S0`.
- [x] Local/API-key authentication disabled on the Azure OpenAI account.
- [x] `Cognitive Services OpenAI User` assigned to the lab user at the account scope.
- [x] Regional `Standard` deployment of `gpt-4.1-mini` version `2025-04-14` created.
- [x] Deployment capacity limited to 1K TPM / 1 RPM, with dynamic quota disabled.
- [x] Automatic model-version upgrades disabled; content filter set to `DefaultV2`.
- [x] Microsoft Entra ID inference access verified without an API key.
- [x] Azure OpenAI v1 endpoint returned HTTP 200 for a minimal Responses API request.

Phase 0 is complete. The response confirmed the deployment name, content filtering, token usage, and
successful inference through Microsoft Entra ID. The transient response ID and access token are not
recorded in the repository.

No email address, access token, API key, subscription ID, tenant ID, or other personal identifier belongs
in this repository.

## Fixed Phase 0 choices

- Resource group: `rg-llm-observability-lab`.
- Region: Sweden Central.
- Account kind: Azure OpenAI.
- Account SKU: `S0`.
- Authentication: Microsoft Entra ID only; local/API-key authentication disabled.
- Public network access: enabled temporarily for local development.
- Model: `gpt-4.1-mini`, version `2025-04-14`.
- Deployment SKU: regional `Standard`.
- Deployment capacity: `1`, equivalent to 1,000 TPM for this model.
- Dynamic quota: disabled.
- Version upgrades: disabled so experiments remain reproducible.
- Content filter: `Microsoft.DefaultV2`.

The live catalog currently classifies the pinned model version as `Legacy`, with inference support through
2027-04-14. Replacing it requires revalidating the experiments and their expected telemetry.

## Phase 1 application checkpoint

State verified on 2026-09-04:

- [x] Native OpenAPI document exposed in Development.
- [x] Scalar API reference exposed in Development.
- [x] `AskChatUseCase` isolated behind `IAskChatUseCase` and `IChatClient`.
- [x] Azure OpenAI client authenticated with Microsoft Entra ID through `DefaultAzureCredential`.
- [x] `POST /api/chat` exposed through an MVC controller.
- [x] End-to-end request verified manually through Scalar with HTTP 200.
- [x] Automated use-case tests remain network-free.
- [ ] Add explicit prompt validation.
- [ ] Add the local OpenTelemetry and Aspire Dashboard pipeline.

The endpoint and deployment name are supplied through configuration. No Azure credential is stored in
the repository.
