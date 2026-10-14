# Design

## Context

Locally, the AppHost wires everything together:
- each API (`games-api`, `users-api`, `events-api`) runs with a Dapr sidecar;
- the `pubsub` component sits on RabbitMQ and carries the single topic `integration-events`;
- the `game-cleanup` cron binding calls the Games API;
- tables live in Azurite (connection name `tables`, read by `AddAzureTableServiceClient`);
- `Users__Tokens__SigningKey` is a generated parameter;
- CORS comes from `Gateway__AllowedOrigins` (module APIs) and `Events__AllowedOrigins__*` (Events API);
- the YARP `gateway` is built from `GatewayRoutes.Modules`.

The frontend loads `backendBaseUrl` from `public/config.json` (written by `scripts/aspire-start.mjs`), but `EVENTS_API_URL` is hard-coded to `http://localhost:5317` in `app.config.ts`.

ServiceDefaults exports OTLP only when `OTEL_EXPORTER_OTLP_ENDPOINT` is set. The Azure Monitor line is commented out, and health endpoints are mapped only in Development.

`ci.yml` already shows what the test jobs need: .NET 10, an Azurite service for table tests, and `dapr init --slim` for the AppHost tests. The `production` GitHub environment holds the `AZURE_CLIENT_ID`, `AZURE_TENANT_ID` and `AZURE_SUBSCRIPTION_ID` secrets, and the `ACR_LOGIN_*` secrets are repository-level.

See proposal.md for motivation.

## Goals / Non-Goals

**Goals:**
- One workflow file, three stages (verify, publish, deploy), maximum parallelism inside each stage.
- Infrastructure fully reproducible from `infra/` with one subscription-scope deployment.
- Production behaviour of the services equals local behaviour: same Dapr component names, topic, app ids and route table.
- The same frontend bundle works in every environment (runtime config only).

**Non-Goals:**
- Preview or staging environments, custom domains, private networking (VNet integration) and WAF.
- Deploying through `aspire publish` or `azd`. Bicep is hand-written as requested.
- Making the Events API horizontally scalable.
- Health or readiness endpoints in production. Container Apps' default TCP probes are kept, and exposing `/health` outside Development is a separate decision.

## Decisions

### D1. Workflow shape: `.github/workflows/deploy.yml`

- **Triggers:** `push` to `main` and `workflow_dispatch`.
- **Concurrency:** group `deploy-production` with `cancel-in-progress: false`, so two releases never deploy at once and a running deployment is never cut off.
- **Jobs:**
  - `version`: runs GitVersion and outputs `semVer`.
  - Stage 1, matrix `verify-backend` over the backend module folders (`Users`, `Games`, `Events`, `Core`, `Shared`, `Aspire`, `DevTools`). Each leg restores the solution and runs `dotnet test` on that module's test project in Release with `--filter "Category!=Integration"`. Azurite is a job service and Dapr slim is installed, as in `ci.yml`.
  - Stage 1, `verify-frontend`: `npm ci`, build and test.
  - Stage 2, `needs: [version, verify-*]`:
    - matrix `publish-api` over `games`, `users`, `events`;
    - `build-frontend`: `ng build --configuration production`, uploading `dist/.../browser` as artifact `frontend`.
  - Stage 3, `deploy`: `needs` every publish job, `environment: production`.
- **Why a module matrix rather than one solution test:** the issue asks for modules compiled and tested simultaneously, and a matrix shows which module failed.
- **Alternatives:**
  - Reusing `ci.yml` through `workflow_call`. Rejected because CI tests the whole solution in one job, and the issue says not to change CI's behaviour.
  - One job per test project. Rejected as too many legs for no gain.

### D2. Semantic versioning with GitVersion

- **Tooling:** `gittools/actions` `setup` and `execute`, plus a `GitVersion.yml` at the repository root.
- **Branch config:** `main` uses `ContinuousDeployment` with patch increments, and conventional-commit bump messages (`+semver: minor`, `feat:` and so on, through `major-version-bump-message` and `minor-version-bump-message`).
- **Tagging:** after a successful deploy the workflow pushes the tag `v<semVer>` (`permissions: contents: write`). The next run then starts from the released version, and releases are visible in git.
- **Alternatives:**
  - `major.minor` from a file plus `github.run_number`. Rejected because it is not derived from history and has no tags.
  - Semantic-release in Node. Rejected because it adds a Node toolchain to a .NET versioning concern.

### D3. Images via SDK container publishing

- **Command:** each `*.Api` project is published with `dotnet publish <api>.csproj -c Release -t:PublishContainer -p:ContainerRegistry=$ACR_LOGIN_SERVER -p:ContainerRepository=battleship/<service>-api -p:ContainerImageTag=<semVer>`.
- **Login:** `docker/login-action` runs first with the ACR secrets.
- **Project properties:** the API csproj files get `<IsPublishable>`, `<EnableSdkContainerSupport>` and a `ContainerRepository` default, so a local `dotnet publish -t:PublishContainer` also works.
- **No Dockerfiles** are added.

### D4. Bicep layout

- **Entry point:** `infra/main.bicep` at `targetScope = 'subscription'`. It creates the resource group (`resourceGroupName`, default `battleship-production`; `location`, default `westeurope`) and calls `infra/modules/*.bicep`:
  - `monitoring` (Log Analytics, workspace-based Application Insights)
  - `identity` (user-assigned managed identity)
  - `storage` (Storage Account, `Storage Table Data Contributor` for the identity)
  - `servicebus` (Standard namespace, one topic per entry of `topics`, `Azure Service Bus Data Owner` for the identity)
  - `containerapps-env` (environment with Log Analytics, Dapr components)
  - `containerapp` (reusable module for one app)
  - `gateway`
  - `staticwebapp`
- **Parameters:** `infra/main.bicepparam` holds the non-secret values. `version`, `acrLoginServer`, `acrUsername`, `acrPassword` and `signingKey` come from the workflow; the last two are `@secure()`.
- **Outputs:** `gatewayUrl`, `eventsApiUrl`, `staticWebAppName` and `staticWebAppHostname`.
- **Naming:** resource names are `<abbr>-battleship-<suffix>`, with `uniqueString(subscription().id, resourceGroupName)` where global uniqueness is needed (storage, Service Bus).

### D5. Container Apps and ingress

- **Names:** one Container App per API with the same name as its Dapr app id: `games-api`, `users-api`, `events-api`.
- **Images:** `<acr>/battleship/<service>-api:<version>`, pulled with a registry entry whose password is a Container Apps secret.
- **Dapr:** enabled with `appId` equal to the app name, `appPort` 8080 and protocol http.
- **Ingress:**
  - Games and Users: internal only (`external: false`).
  - Events: external with sticky sessions off, `minReplicas = maxReplicas = 1`.
  - Gateway: external.
- **Why the version reaches the apps:** changing the image tag creates a new revision. Single-revision mode replaces the old one.

### D6. Gateway in production: the YARP container configured from a generated route file

- **Image:** the deployed gateway uses the same YARP container image the Aspire `AddYarp` integration runs (`mcr.microsoft.com/dotnet/nightly/yarp`, pinned tag).
- **Configuration:** environment variables `ReverseProxy__Routes__<prefix>__Match__Path=/<prefix>/{**catch-all}` and `ReverseProxy__Clusters__<api>__Destinations__d1__Address=http://<api>`, using the internal app name.
- **Route source:** the routes come from `infra/gateway-routes.json` (module, API app name, prefixes), which `gateway.bicep` reads with `loadJsonContent`.
- **Drift test:** a new AppHost test asserts that `infra/gateway-routes.json` lists exactly the modules and prefixes of `GatewayRoutes.Modules`. A prefix added to one side only fails CI, which is the drift scenario in the spec. Updating the file becomes part of the `yarp-gateway-routes` skill's checklist (task 3.4).
- **Alternatives:**
  - A custom gateway project. Rejected because it adds a fourth image and diverges from local.
  - Container Apps path-based routing (preview). Rejected as not GA and outside the route table.
  - Letting the frontend call internal APIs directly. Rejected because they would have to be public.

### D7. Data access with a managed identity

- **Identity:** the user-assigned identity is attached to `games-api`, `users-api` and `events-api`. Its client id is set as `AZURE_CLIENT_ID`.
- **Tables:** `ConnectionStrings__tables` is set to the table endpoint URI (`https://<account>.table.core.windows.net/`). The Aspire Azure Tables client treats a URI connection string as "use `DefaultAzureCredential`", so no code change and no account key.
- **Service Bus:** the Dapr `pubsub` component (`pubsub.azure.servicebus.topics`, version v1) uses `namespaceName: <ns>.servicebus.windows.net` and `azureClientId: <identity client id>`, scoped to the three app ids.
- **Subscriptions:** Dapr creates them itself (entity management on, which is why the role is Data Owner). Its consumer id defaults to the app id, matching local behaviour.
- **Topics:** the Bicep parameter `topics` defaults to `['integration-events']`. It is the single list of topic names, and adding a topic in code means adding it there.
- **`game-cleanup`:** a `bindings.cron` component with `schedule: @daily`, `route: /maintenance/game-cleanup`, `direction: input`, scoped to `games-api`.
- **Alternative:** connection strings with SAS keys. Rejected because they put keys in Container Apps secrets and component metadata.

### D8. Secrets

`signingKey` (from the `USERS_SIGNING_KEY` environment secret) and `acrPassword` are `@secure()` parameters, stored as Container Apps secrets and referenced with `secretRef`. `Users__Tokens__SigningKey` is set on all three APIs.

The workflow passes them as `--parameters` arguments to `azure/arm-deploy` (or `az deployment sub create`), never echoed. GitHub masks them in logs.

Key Vault was considered but adds a resource, an access policy and a reference indirection for two values. It can be adopted later without changing the specs.

### D9. Telemetry

- **APIs:** ServiceDefaults calls `UseAzureMonitor()` on the OpenTelemetry builder when `APPLICATIONINSIGHTS_CONNECTION_STRING` is configured, next to the existing OTLP switch. This adds the package `Azure.Monitor.OpenTelemetry.AspNetCore`, the documented first-party exporter. Its use is limited to ServiceDefaults, in line with the OpenTelemetry skill: instrumentation stays vendor-neutral and only the exporter is Azure-specific.
- **Bicep:** sets the connection string on every API.
- **Dapr:** the Container Apps environment sets `daprAIConnectionString`, so sidecar spans join the same traces.

### D10. CORS and the frontend runtime config

- **CORS:** the gateway Container App's ingress `corsPolicy` allows only `https://<staticWebApp.defaultHostname>`. The module APIs' own gateway CORS (`GatewayCorsExtensions`) is enabled only in Development, so setting `Gateway__AllowedOrigins` in production would have no effect. Answering CORS at the single public entry point keeps the APIs unchanged. Bicep sets `Events__AllowedOrigins__0` on Events, whose CORS policy is active in every environment. The hostname is known within the same deployment, so a first deployment works without a second pass.
- **`config.json`:** gains `eventsBaseUrl`.
  - The backend config loader reads both values.
  - `EVENTS_API_URL` is provided from the loaded config instead of a literal.
  - A missing value logs one error naming the setting, and the events service refuses to connect.
- **Local runs:** the AppHost passes the Events API endpoint to the frontend as `EVENTS_API_URL`, and `aspire-start.mjs` writes it. The `localhost:5317` default goes away, which also fixes live events in `--isolated` runs, where the Events API gets a different port.
- **Deploy:** the deploy job overwrites `config.json` in the downloaded artifact with the Bicep outputs before upload.
- **SPA routing:** a `staticwebapp.config.json` with `navigationFallback` (`/index.html`, excluding assets and `config.json`) is added to `public/` so deep links work.

### D11. Static Web App upload

- **Resource:** a Free-tier Static Web App, created by Bicep without a repository link.
- **Upload:** the deploy job reads the deployment token with `az staticwebapp secrets list --name <name> --query properties.apiKey -o tsv`, masks it, and runs `Azure/static-web-apps-deploy@v1` with `action: upload`, `skip_app_build: true` and `app_location` pointing at the downloaded artifact.
- **Why:** no extra GitHub secret is needed, and the token never leaves the job.

## Risks / Trade-offs

- **Azure permissions.** The OIDC principal needs rights to create resource groups and role assignments, so the workflow fails at the first role assignment without them. → Document the needed role (Owner, or Contributor plus Role Based Access Control Administrator, on the subscription) and the federated credential subject `repo:hexmasternl/battleship:environment:production` in `infra/README.md`. Task 7.1 checks them before the first run.
- **`USERS_SIGNING_KEY` is missing.** It does not exist in the `production` environment yet. → The deploy job fails fast with a clear message when it is empty. The README states how to generate one: 48+ characters with no special characters, the same rule as the AppHost.
- **SSE through Container Apps ingress.** Envoy idle timeouts can drop long-lived streams. → The Events API already sends heartbeats and the client reconnects with `Last-Event-ID`. Verify on the deployed app that streams survive minutes of idle play.
- **Dapr entity management needs Data Owner.** That is broader than send and listen. → Accepted for a single-app namespace. Pre-creating subscriptions and dropping to Sender/Receiver is a follow-up.
- **Cost.** Service Bus Standard is needed for topics and is the main fixed cost. → Container Apps consumption with scale-to-zero on Users (`minReplicas: 0`). Games stays at one replica because the `game-cleanup` cron binding runs in its sidecar, and Events stays at exactly one.
- **YARP nightly image.** It can change under a floating tag. → Pin an explicit tag in Bicep.
- **Out of scope here.** OpenAPI and Scalar are not exposed in production because the module APIs are internal.

## Migration Plan

1. **Before the first run:**
   - Add `USERS_SIGNING_KEY` to the `production` environment.
   - Make sure the Entra app behind `AZURE_CLIENT_ID` has a federated credential for the `production` environment and the subscription role above.
2. **First run:** merge to `main`. The deployment creates the resource group and all resources, then uploads the frontend.
3. **Rollback:**
   - Rerun the `deploy` job of an earlier successful run from the Actions UI. Its images and frontend artifact are still in the registry and artifact store, and redeploying moves every Container App back to that version.
   - Data is not migrated by releases, so rollback is safe.
4. **Teardown:** delete the resource group. The ACR is untouched.
