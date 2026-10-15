# Proposal

> Tracks GitHub issue [hexmasternl/battleship#75](https://github.com/hexmasternl/battleship/issues/75).

## Why

Production runs an extra YARP Container App only to fan requests out to the Users and Games APIs, and the frontend lives on a generated `*.azurestaticapps.net` hostname. Azure Container Apps can do that routing on the environment itself from the same prefix table the local gateway uses, so the gateway app, its image and its CORS ingress can go, and the app gets its real address, `battleship.hexmaster.nl`.

## What Changes

- **Custom hostname for the frontend.** The Static Web App gets the custom domain `battleship.hexmaster.nl`, declared in Bicep and validated through a CNAME record that is created by hand in the DNS zone (documented in `infra/README.md`). Every place that used the default hostname as the frontend origin uses `https://battleship.hexmaster.nl` instead.
- **BREAKING: the `gateway` Container App is removed.** `infra/modules/gateway.bicep`, its module in `environment.bicep`, the YARP image in production and the `gatewayUrl` deployment output disappear. The old gateway URL stops working after the first deployment of this change.
- **Request routing on the Container Apps environment.** An HTTP route configuration on `cae-battleship-production` routes each prefix of the shared route table (`/users`, `/games`) to the module's internal Container App with the path unchanged, generated from `infra/gateway-routes.json` exactly as the gateway was. Its FQDN is the frontend's backend URL. The Users and Games apps keep internal ingress.
- **CORS moves into the backend.** The Users and Games APIs register their CORS policy in every environment, not only in Development, from the configured allowed origins; the deployment passes the frontend origin to both. The Events API's allowed origin moves to the custom hostname. No wildcard origins.
- **The backend endpoint reaches the frontend from the deployment.** The route configuration FQDN is a Bicep output that the deploy job writes into the frontend's runtime `config.json` as `backendBaseUrl`, in the same run. Static Web App application settings were considered and rejected: Azure exposes them only to the Static Web App's API backend, not to static content, so the Angular app cannot read them (see design).
- **A new skill keeps local and deployed routing in sync.** `container-apps-route-sync` (Claude Code and Copilot copies) owns `infra/gateway-routes.json` and the Bicep that turns it into environment routes, and verifies that modules, prefixes and target apps match `GatewayRoutes` in the AppHost, including a check against the live route configuration. The `yarp-gateway-routes` skill keeps owning `GatewayRoutes.cs` and hands the deployment copy to the new skill. `CLAUDE.md` and `.github/copilot-instructions.md` name both.
- **Workflow and docs.** `deploy.yml` reads the route configuration FQDN instead of the gateway URL and prints `https://battleship.hexmaster.nl` as the app URL; `infra/README.md` describes the new resource list, the DNS record and the hostname parameter.
- **The event stream goes through the proxies too** (added during implementation, route approved on 2026-10-15). The route table gains the `events` module with prefix `/events`, so the local YARP gateway and the environment route configuration forward the server-sent event stream to the Events API. The stream's 15-second heartbeat keeps it below both proxies' idle timeouts. The frontend's `eventsBaseUrl` points at the gateway locally and at the backend route in Azure, where the Events app loses its public address. Dapr's `/integration-events` stays unrouted.

Local development keeps its YARP `gateway` from `GatewayRoutes`, now routing three modules, and the APIs keep allowing the frontend origin of the run.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `azure-hosting`: the APIs run as Container Apps without a gateway app; the environment's route configuration, not a gateway, routes the shared prefix table to the module apps; cross-origin requests are allowed only from `https://battleship.hexmaster.nl` and answered by the APIs themselves; the frontend is served on the custom hostname. This capability is introduced by the in-flight change `issue-65-azure-deployment` (merged, awaiting archive); that change must be archived before this one so the delta applies to an existing spec.
- `continuous-deployment`: the deploy stage writes the environment route configuration URL, not a gateway URL, into the hosted frontend's runtime configuration.

## Impact

- `infra/`: `modules/staticwebapp.bicep` (custom domain), `modules/environment.bicep` (route configuration module, frontend origin from the hostname parameter, API CORS environment variables, outputs), new `modules/routing.bicep`, removed `modules/gateway.bicep`, `modules/containerapp.bicep` (unused `corsAllowedOrigins` parameter removed), `modules/containerapps-env.bicep` (API version and, if required by rule-based routing, workload profiles), `main.bicep` and `main.bicepparam` (hostname parameter, outputs), `README.md`.
- `src/Aspire/.../ServiceDefaults/GatewayCorsExtensions.cs` and `GatewayCorsExtensionsTests.cs`: policy registered whenever origins are configured, in every environment.
- `src/Aspire/.../Tests/GatewayRoutesFileTests.cs`: keeps the routes file in step with `GatewayRoutes`, extended to the Bicep that consumes it.
- `.github/workflows/deploy.yml`: new output names, `config.json` content, summary.
- `.claude/skills/container-apps-route-sync/`, `.github/skills/container-apps-route-sync/`, both `yarp-gateway-routes` copies, `CLAUDE.md`, `.github/copilot-instructions.md`.
- Azure: the `gateway` Container App is deleted on the first deployment; the environment may have to be recreated if rule-based routing requires a workload-profiles environment (see design, Risks). The DNS zone `hexmaster.nl` needs a CNAME for `battleship` before that deployment.
- `src/Aspire/.../AppHost/GatewayRoutes.cs` and `AppHost.cs`: the `events` module and its `/events` prefix; the frontend's Events URL becomes the gateway endpoint. `FrontendResourceTests` and `GatewayRoutesTests` follow.
- Not affected: the frontend code (it keeps reading `config.json` and appending `/events`), the module APIs' endpoints.
