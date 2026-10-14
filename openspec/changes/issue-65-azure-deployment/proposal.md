# Proposal

> Tracks GitHub issue [hexmasternl/battleship#65](https://github.com/hexmasternl/battleship/issues/65).

## Why

The game only runs locally through the Aspire AppHost: there is no infrastructure-as-code, no published container image and no hosted frontend, so nobody can play it without cloning the repository. A push to `main` should ship a playable version to Azure without manual steps.

## What Changes

- New GitHub Actions workflow `deploy.yml`, triggered by a push to `main`, that runs in three stages:
  1. builds and tests every backend module and the frontend in parallel jobs;
  2. when all succeed, publishes one container image per API with `dotnet publish` (`battleship/games-api`, `battleship/users-api`, `battleship/events-api`, tagged with one semantic version computed per run) to the existing registry in `ACR_LOGIN_SERVER`, and in parallel builds the frontend and stores it as a workflow artifact;
  3. in the GitHub `production` environment, logs in to Azure with OIDC, deploys the Bicep templates with the new version so every Container App moves to it, and uploads the frontend artifact to the Static Web App.
- New Bicep templates under `infra/` provisioning: Log Analytics, Application Insights, an Azure Container Apps environment, Container Apps for the three APIs and the YARP gateway, an Azure Service Bus namespace with the topic `integration-events`, Dapr components `pubsub` (Service Bus topics) and `game-cleanup` (cron) on the environment, a Storage Account for table storage, a user-assigned identity for data access, and an Azure Static Web App.
- The APIs export telemetry to Application Insights when a connection string is configured (ServiceDefaults).
- The frontend reads the Events API URL from its runtime `config.json` next to the gateway URL, instead of the hard-coded `http://localhost:5317`. The AppHost passes the local Events API URL the same way, and the deploy writes both deployed URLs into `config.json` before upload.
- The gateway's deployed route table is generated from, and tested against, the same `GatewayRoutes` table the AppHost uses.

## Capabilities

### New Capabilities
- `continuous-deployment`: the pipeline that turns a push to `main` into a deployed release: trigger, build and test gate, semantic versioning, image naming and publishing, frontend artifact and the deploy stage.
- `azure-hosting`: what the production environment in Azure consists of and how the services are configured there: hosted resources, messaging and Dapr components, storage, secrets, CORS, telemetry and the single-instance Events API.

### Modified Capabilities
- `frontend-backend-access`: the runtime configuration file also carries the Events API URL, and it is produced both by the Aspire start script and by the deployment.

## Impact

- **New files**: `.github/workflows/deploy.yml`, `infra/` (Bicep modules and parameters), `GitVersion.yml`.
- **Code**: `HexMaster.Battleship.Aspire.ServiceDefaults` (Azure Monitor exporter), the three `*.Api` projects (container publish properties), the AppHost (passes the Events API URL to the frontend), `src/Frontend` (`config.json` shape, `app.config.ts`, `scripts/aspire-start.mjs`).
- **Services**: aspire, games, users, events, frontend. DevBot is not deployed.
- **Dependencies**: `Azure.Monitor.OpenTelemetry.AspNetCore` in ServiceDefaults; GitHub actions `gittools/actions`, `azure/login`, `Azure/static-web-apps-deploy`.
- **Secrets**: existing `ACR_LOGIN_*` repository secrets and `AZURE_*` OIDC secrets in the `production` environment; a new `USERS_SIGNING_KEY` secret in `production` must be added before the first run.
- **Unchanged**: `ci.yml`, local development with Aspire, RabbitMQ and Azurite.
