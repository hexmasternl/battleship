# Tasks

## 1. Frontend runtime configuration for the Events API

- [ ] 1.1 Extend the backend config loader to read `eventsBaseUrl` from `config.json` next to `backendBaseUrl`, and log one error naming the setting when it is missing. Verify with updated `backend-config.spec.ts` cases (`ng test --include`).
- [ ] 1.2 Provide `EVENTS_API_URL` from the loaded config instead of `http://localhost:5317` in `app.config.ts`. The events service must not open a stream when the URL is missing. Verify with events service specs covering both cases.
- [ ] 1.3 Make `scripts/aspire-start.mjs` write `eventsBaseUrl` from a new `EVENTS_API_URL` environment variable, and have the AppHost pass the Events API HTTP endpoint to the frontend resource. Verify with the script's tests and an AppHost test asserting the environment variable, then a local `aspire start` with live events working.
- [ ] 1.4 Add `public/staticwebapp.config.json` with `navigationFallback` to `/index.html`, excluding assets and `config.json`. Verify it appears in the `ng build --configuration production` output.

## 2. Telemetry and container publishing in the services

- [x] 2.1 Add `Azure.Monitor.OpenTelemetry.AspNetCore` to ServiceDefaults and call `UseAzureMonitor()` only when `APPLICATIONINSIGHTS_CONNECTION_STRING` is set. Verify with ServiceDefaults tests for the set and unset cases; the OTLP behaviour must be unchanged.
- [x] 2.2 Enable SDK container support on `Games.Api`, `Users.Api` and `Events.Api` with `ContainerRepository` defaults `battleship/<service>-api`. Verify that `dotnet publish -t:PublishContainer -p:ContainerArchiveOutputPath=...` produces an image tarball per API locally.

## 3. Gateway route file for production

- [x] 3.1 Add `infra/gateway-routes.json` listing each module, its API app name (`games-api`, `users-api`) and its prefixes, matching `GatewayRoutes.Modules`. Add an AppHost test that fails when the two disagree, in either direction. Verify that the test passes, and fails after a temporary mismatching edit.
- [x] 3.2 Update the `yarp-gateway-routes` skill (and its Copilot mirror via `sync-agent-config`) so that adding, renaming or removing a prefix also updates `infra/gateway-routes.json`. Verify with the sync-agent-config check.

## 4. Bicep infrastructure

- [x] 4.1 Create `infra/main.bicep` (subscription scope: resource group plus module calls), `infra/main.bicepparam` and the `monitoring`, `identity`, `storage` and `servicebus` modules. The Service Bus module gets a topics parameter defaulting to `integration-events`, and the role assignments go to the identity. Verify with `az bicep build` and `az bicep lint` (no errors).
- [ ] 4.2 Add the `containerapps-env` module: Log Analytics logs, `daprAIConnectionString`, the Dapr `pubsub` component (Service Bus topics, managed identity, scoped to the three app ids) and the `game-cleanup` cron component scoped to `games-api`. Verify with `az bicep build`.
- [ ] 4.3 Add the reusable `containerapp` module and instantiate `games-api` and `users-api` (internal), plus `events-api` (external, exactly one replica). Each gets: the registry secret, Dapr settings, the identity, `AZURE_CLIENT_ID`, `ConnectionStrings__tables` as the table endpoint URI, `Users__Tokens__SigningKey` via `secretRef`, the App Insights connection string, and the CORS origin of the Static Web App. Verify with `az bicep build`.
- [ ] 4.4 Add the `gateway` module, which runs the pinned YARP image with routes and clusters generated from `gateway-routes.json` via `loadJsonContent`, and the `staticwebapp` module (Free tier). Expose the outputs `gatewayUrl`, `eventsApiUrl`, `staticWebAppName` and `staticWebAppHostname`. Verify with `az bicep build` and with `az deployment sub what-if` against the production subscription, which must show only creates.
- [ ] 4.5 Write `infra/README.md` covering resources, parameters, required GitHub secrets (`USERS_SIGNING_KEY` and how to generate it), the federated credential subject, the required Azure role, rollback and teardown. Verify that every secret and parameter named in the templates and workflow is documented.

## 5. Versioning

- [ ] 5.1 Add `GitVersion.yml` (`main` in ContinuousDeployment with patch increments, and conventional-commit bump messages for minor and major). Verify that running GitVersion locally (`dotnet-gitversion`) prints a `SemVer` higher than the latest existing tag.

## 6. Deployment workflow

- [ ] 6.1 Create `.github/workflows/deploy.yml` with the `push: main` and `workflow_dispatch` triggers, the `deploy-production` concurrency group, the `version` job, and the stage 1 jobs: the `verify-backend` matrix over modules (with Azurite and Dapr slim, as in `ci.yml`) and `verify-frontend`. Verify with `actionlint` and that the job graph shows the parallel jobs.
- [ ] 6.2 Add stage 2: the `publish-api` matrix (ACR login, then `dotnet publish -t:PublishContainer` with the version tag) and `build-frontend` (production build uploaded as artifact `frontend`), both needing every stage 1 job. Verify with `actionlint`.
- [ ] 6.3 Add stage 3 `deploy` in environment `production`, with `id-token: write` and `contents: write`. It needs: a fail-fast check for `USERS_SIGNING_KEY`, OIDC `azure/login`, a subscription deployment of `infra/main.bicep` with the version and secure parameters, `config.json` written into the downloaded artifact from the outputs, the masked SWA token, `static-web-apps-deploy` upload, and the `v<semVer>` tag push. Verify with `actionlint` and that `ci.yml` is unchanged (`git diff --stat`).

## 7. End-to-end verification

- [ ] 7.1 Confirm that the `production` environment holds `USERS_SIGNING_KEY` and that the OIDC principal has the federated credential and subscription role (ask the user if not). Then run the workflow with `workflow_dispatch` from the change branch, or after merge. Verify that every job is green, the registry holds the three images with the run's version, and every Container App revision runs that tag.
- [ ] 7.2 On the Static Web App URL, verify the following and record the results in the pull request:
  - Sign in, then create and join a game from two browsers.
  - Play shots and see them arrive live through the event stream.
  - A deep link loads the app.
  - `<gateway>/maintenance/game-cleanup` is not routed.
  - A trace for game creation appears in Application Insights.
