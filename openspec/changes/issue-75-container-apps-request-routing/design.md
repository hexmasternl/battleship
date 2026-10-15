# Design

## Context

See proposal.md for the motivation. The pieces this design rearranges:

- `infra/gateway-routes.json` already mirrors `GatewayRoutes.Modules` (module, Container App name, prefixes) and `GatewayRoutesFileTests` fails when they differ. `infra/modules/gateway.bicep` turns it into YARP `ReverseProxy__*` environment variables for the `gateway` Container App, whose ingress also answers CORS for the Static Web App's default hostname.
- `users-api` and `games-api` have internal ingress. `GatewayCorsExtensions` registers a CORS policy from `Gateway:AllowedOrigins` only in Development; the AppHost sets it to the frontend endpoint of the run. The Events API keeps its own `Events:AllowedOrigins` policy and is public.
- The frontend reads `backendBaseUrl` and `eventsBaseUrl` from a runtime `config.json` that `deploy.yml` writes from the Bicep outputs; the bundle never contains a URL.
- Azure Container Apps rule-based routing: a `Microsoft.App/managedEnvironments/httpRouteConfigs` child resource (API version `2024-10-02-preview`) with ordered rules, each with `routes[].match` (`prefix`, `pathSeparatedPrefix` or `path`), an optional `action.prefixRewrite` and `targets[].containerApp`. It gets its own FQDN under the environment domain and may target apps with internal ingress. Microsoft's Bicep sample declares the environment with a Consumption workload profile.
- Static Web Apps: custom domains are `Microsoft.Web/staticSites/customDomains` resources, available on the Free plan, validated by CNAME for a subdomain. Application settings reach only the API backend of a Static Web App, not static files.

## Goals / Non-Goals

**Goals:**
- One source for routes on both sides: `GatewayRoutes` for the AppHost, `gateway-routes.json` for Azure, the same shape, enforced by tests and by a skill that can also compare with the live environment.
- Every backend address the frontend needs comes out of the Bicep deployment that created it, in the same workflow run.
- Cross-origin access is decided by the APIs from configuration, identically in Development and production; only the configured origins differ.

**Non-Goals:**
- Routing the Events API stream through the environment routes or a custom hostname for the backend.
- Reading configuration from Static Web App application settings in the browser.
- Any change to the local YARP gateway, the frontend interceptor or `config.json` shape.

## Decisions

### D1. Environment route configuration generated from `gateway-routes.json`
A new `infra/modules/routing.bicep` loads `../gateway-routes.json` with `loadJsonContent` (as `gateway.bicep` does today) and declares one `httpRouteConfigs` resource named `backend` on the environment. Each module becomes one rule; each prefix one route with `match.pathSeparatedPrefix: '/<prefix>'`, `caseSensitive: true` and no `prefixRewrite`, so `/users/me` reaches `users-api` as `/users/me`, matching the YARP route `/{prefix}/{**catch-all}` and the `local-gateway` spec. Target is `containerApp: <module.app>`. The module outputs `https://<fqdn>` as `backendUrl`. Alternatives: keep a YARP app (rejected by the issue); hand-written rules (would drift from the table and bypass the test); Azure Front Door or API Management (cost and a second routing table).

Ordering: all prefixes are single segments and unique (validated by `GatewayRoutes.Validate`), so rule order cannot shadow another rule. The Bicep keeps the file order.

### D2. Route configuration name and address
The resource is named `backend`, giving `backend.<environment-default-domain>`. The name is stable across deployments, so the address only changes when the environment is recreated. The deploy job keeps writing it into `config.json`; nothing else stores it.

### D3. Environment API version and workload profiles
`containerapps-env.bicep` moves to API version `2024-10-02-preview` for the route configuration child resource. Rule-based routing is documented with a workload-profiles environment (`workloadProfiles: [{ name: 'Consumption', workloadProfileType: 'Consumption' }]`). The implementation first runs `az deployment sub what-if` with the route configuration against the existing consumption-only environment; if Azure rejects the route configuration on that environment type, the environment is declared with the Consumption workload profile. An environment cannot be switched in place, so that path means deleting `cae-battleship-production` (and its Container Apps) and letting the next deployment recreate it. Tables (Storage) and topics (Service Bus) live outside the environment, so no game data is lost; players are signed out only if the signing key changes, which it does not. See Risks.

### D4. CORS answered by the APIs, configured from Bicep
`GatewayCorsExtensions.AddGatewayCors` registers the policy whenever `Gateway:AllowedOrigins` is non-empty, in every environment, and `UseGatewayCors` applies it whenever the policy is registered. Without configured origins no policy exists and no cross-origin request is accepted, which keeps today's production behaviour for an unconfigured API. The configuration key keeps its name so the AppHost, the frontend start script and existing tests do not change. `environment.bicep` adds `Gateway__AllowedOrigins = https://battleship.hexmaster.nl` to `users-api` and `games-api`, and `Events__AllowedOrigins__0` moves to the same value. The `corsAllowedOrigins` parameter of `containerapp.bicep` loses its only consumer and is removed. Alternative: the Container App ingress `corsPolicy` on the two module apps (Bicep only). Rejected because the issue asks for backend configuration, because the application policy is the one covered by tests, and because ingress CORS behaviour behind an environment route is not documented.

### D5. Frontend origin as a parameter
`main.bicep` gets `param frontendHostname string = 'battleship.hexmaster.nl'`, set in `main.bicepparam`. `environment.bicep` derives `frontendOrigin = 'https://${frontendHostname}'` from it instead of from the Static Web App output. `staticwebapp.bicep` declares `Microsoft.Web/staticSites/customDomains` with `name: frontendHostname` and `validationMethod: 'cname-delegation'`, depending on the site. The CNAME `battleship -> <default hostname>` must exist before the first deployment; the README documents it, and the default hostname stays a deployment output so the record can be looked up. Alternative: binding the hostname to the route configuration too (rejected, out of scope; the backend keeps its Azure address).

### D6. Static Web App application settings are not used for the backend URL
Azure documents application settings as available only to the Static Web App's API backend. The frontend has no API backend and reads `config.json`, so a setting would never reach it. The deploy job keeps generating `config.json` from the `backendUrl` and `eventsApiUrl` outputs. Static Web App route rules and linked backends were considered in the issue and rejected (no proxying to another host; Standard plan, `/api` only, one Container App, 45 s limit).

### D7. The `container-apps-route-sync` skill
A new skill in `.claude/skills/container-apps-route-sync/SKILL.md`, copied verbatim to `.github/skills/container-apps-route-sync/SKILL.md`, owns the deployment side of routing:
- Files: `infra/gateway-routes.json` and `infra/modules/routing.bicep`.
- Regenerate: rewrite `gateway-routes.json` from `GatewayRoutes.Modules` (module, app = resource name without `hexmaster-battleship-`, prefixes) and check that `routing.bicep` still reads it and emits one `pathSeparatedPrefix` route per prefix without rewrite.
- Verify: run the Aspire tests (`GatewayRoutesFileTests`), and, when Azure CLI is signed in, `az containerapp env http-route-config show --name cae-battleship-production --resource-group battleship-production --http-route-config-name backend` and compare rules, prefixes and targets with the file; report differences per module and prefix.
- Triggers: `GatewayRoutes.cs` or `gateway-routes.json` edited, a module or prefix proposal approved in `yarp-gateway-routes`, "is routing in sync", "regenerate the routes".
The `yarp-gateway-routes` skill (both copies) keeps the proposal and approval flow and `GatewayRoutes.cs`, and its "After approval" step delegates the deployment copy to the new skill instead of editing `gateway-routes.json` itself. `CLAUDE.md` and `.github/copilot-instructions.md` add one paragraph for the new skill. `sync-agent-config` check must pass before the commit that adds them.

### D8. Tests
- `GatewayRoutesFileTests` stays as the drift guard for the JSON. One test is added that `infra/modules/routing.bicep` references `gateway-routes.json` and uses `pathSeparatedPrefix` without `prefixRewrite`, so a rewrite of the Bicep that stops consuming the table fails the build. The Bicep file is linked into the test output like the JSON.
- `GatewayCorsExtensionsTests`: the "outside Development registers no policy" test becomes "outside Development with configured origins allows them" plus "outside Development without origins registers no policy"; the preflight test runs in Production.
- Bicep: `az bicep build --file infra/main.bicep` in the worktree, and `az deployment sub what-if` by the user before merging (the workflow does not run what-if).

## Risks / Trade-offs

- [Rule-based routing is in preview and may require a workload-profiles environment] → D3: probe with what-if first; if recreation is needed, do it in a maintenance window, documented in the README's deployment notes. Data is outside the environment.
- [First deployment fails on the custom domain when the CNAME is missing] → the README and the pull request say the record must exist first; the custom domain resource is the last thing the Static Web App module creates, so the rest of the deployment is unaffected by its failure being retried.
- [Managed certificate issuance takes minutes after validation] → acceptance is checked after propagation; no code impact.
- [Removing the gateway breaks anyone using the old gateway URL] → the only consumer is the deployed frontend, rewritten in the same run. Called out as BREAKING in the proposal.
- [CORS now depends on an environment variable per API] → both APIs get it from one `frontendOrigin` variable in `environment.bicep`; the tests prove the policy is applied outside Development; a missing value fails closed.
- [Route configuration FQDN changes if the environment is recreated] → it is never stored; every deployment rewrites `config.json`.

## Migration Plan

1. Create the CNAME `battleship.hexmaster.nl -> <Static Web App default hostname>` in the DNS zone.
2. Merge and deploy. The deployment adds the custom domain, the route configuration and the API CORS variables, and removes the `gateway` Container App (ARM incremental mode keeps resources, so the deploy job deletes it explicitly with `az containerapp delete` when it still exists).
3. If D3 required recreating the environment, delete the environment before step 2 and run the deployment twice if the Static Web App validation outruns the DNS.
4. Rollback: re-run the previous workflow's deploy job. It redeploys the gateway app and the old `config.json`; the custom domain and route configuration stay but are unused.
