# Tasks

## 1. Frontend custom hostname

- [x] 1.1 Add `frontendHostname` (default `battleship.hexmaster.nl`) to `infra/main.bicep` and `infra/main.bicepparam`, pass it to `modules/environment.bicep`, and derive `frontendOrigin` from it instead of the Static Web App output; verify `az bicep build --file infra/main.bicep` succeeds and the Events API still receives `Events__AllowedOrigins__0 = https://battleship.hexmaster.nl` in the built template
- [x] 1.2 Declare the custom domain in `infra/modules/staticwebapp.bicep` (`Microsoft.Web/staticSites/customDomains`, `validationMethod: cname-delegation`) and keep `defaultHostname` as an output; verify `az bicep build` succeeds and the custom domain depends on the site
- [x] 1.3 Document the DNS record in `infra/README.md` (Azure setup: CNAME `battleship` to the Static Web App default hostname, created before the first deployment, how to read the default hostname from the deployment outputs) and add the parameter to the parameters table; verify the README reads correctly as rendered Markdown

## 2. Environment request routing

- [x] 2.1 Create `infra/modules/routing.bicep`: load `../gateway-routes.json`, declare the `backend` HTTP route configuration on the environment with one rule per module, one `pathSeparatedPrefix` route per prefix, `caseSensitive: true`, no `prefixRewrite`, target `containerApp: <app>`, and output `backendUrl`; verify `az bicep build` succeeds and the built JSON contains routes for `/users` and `/games` targeting `users-api` and `games-api`
- [x] 2.2 Move `infra/modules/containerapps-env.bicep` to API version `2024-10-02-preview`, expose what `routing.bicep` needs, and wire the module into `environment.bicep` after the Users and Games apps; verify `az bicep build` succeeds with no warnings about the child resource
- [x] 2.3 Run `az deployment sub what-if` against production with the route configuration (user signed in); if Azure rejects rule-based routing on the consumption-only environment, declare the Consumption workload profile and record the environment recreation steps in `infra/README.md`; verify what-if reports the route configuration as a create
- [x] 2.4 Remove `infra/modules/gateway.bicep`, the `gateway` module in `environment.bicep`, the `gatewayUrl` outputs in `environment.bicep` and `main.bicep` (replaced by `backendUrl`), and the unused `corsAllowedOrigins` parameter and `corsPolicy` block in `containerapp.bicep`; verify `az bicep build` succeeds and `grep -ri gateway infra/modules` returns only comments that describe the route configuration
- [x] 2.5 Add a test to `GatewayRoutesFileTests` (link `infra/modules/routing.bicep` into the test output like the JSON) asserting the Bicep loads `gateway-routes.json`, uses `pathSeparatedPrefix` and has no `prefixRewrite`; verify `dotnet test --project src/Aspire/HexMaster.Battleship.Aspire/HexMaster.Battleship.Aspire.Tests` passes
- [x] 2.6 Update the resource table and files section of `infra/README.md`: no gateway row, a row for the `backend` route configuration, `routing.bicep` described, rollback notes adjusted; verify every file and resource the README names exists in `infra/`

## 3. Backend CORS

- [x] 3.1 Change `GatewayCorsExtensions` so `AddGatewayCors` registers the policy whenever `Gateway:AllowedOrigins` has at least one origin, in every environment, and `UseGatewayCors` applies it whenever the policy is registered; update the class comment; verify the solution builds
- [x] 3.2 Update `GatewayCorsExtensionsTests`: configured origins are allowed in Production, no origins in Production registers no policy, preflight from the allowed origin is answered in Production, and the Development cases keep passing; verify `dotnet test --project src/Aspire/HexMaster.Battleship.Aspire/HexMaster.Battleship.Aspire.Tests` passes
- [x] 3.3 In `infra/modules/environment.bicep` add `Gateway__AllowedOrigins = frontendOrigin` to the `users-api` and `games-api` environment lists; verify the built template shows the variable on both apps and on no other app

## 4. Deploy workflow

- [x] 4.1 In `.github/workflows/deploy.yml` read `backendUrl` instead of `gatewayUrl`, write it as `backendBaseUrl` into `frontend/config.json`, delete the `gateway` Container App when it still exists (`az containerapp delete --yes` guarded by `az containerapp show`), and print the app as `https://battleship.hexmaster.nl`, the backend URL and the Events API URL in the summary; verify with `actionlint` or `gh workflow view` that the workflow parses and that no step references `gateway-url`

## 5. Route sync skill

- [x] 5.1 Write `.claude/skills/container-apps-route-sync/SKILL.md` (owns `infra/gateway-routes.json` and `infra/modules/routing.bicep`; regenerate from `GatewayRoutes.Modules`; verify with the Aspire tests and, when signed in, `az containerapp env http-route-config show` compared per module and prefix; triggers and report format) and copy it verbatim to `.github/skills/container-apps-route-sync/SKILL.md`; verify the `sync-agent-config` check reports no drift
- [x] 5.2 Update both copies of `yarp-gateway-routes/SKILL.md`: the deployment copy is owned by `container-apps-route-sync`, "After approval" delegates to it, and the description no longer names `gateway-routes.json` as its own; verify both copies are identical and the `sync-agent-config` check passes
- [x] 5.3 Add a paragraph for the new skill to `CLAUDE.md` and `.github/copilot-instructions.md` next to the gateway routes paragraph, and reword the gateway paragraph so it describes the local gateway and the environment routes; verify both files name both skills with the correct paths

## 7. Event stream through the proxies (route approved 2026-10-15)

- [x] 7.1 Add the `events` module (`hexmaster-battleship-events-api`, prefix `events`) to `GatewayRoutes`, register the Events API in the AppHost's module map so the YARP gateway routes `/events/{**catch-all}` and waits for it, and pass the gateway endpoint to the frontend as `EVENTS_API_URL`; update `GatewayRoutesTests` and `FrontendResourceTests`; verify the Aspire tests pass
- [x] 7.2 Add the `events` module to `infra/gateway-routes.json`, switch `events-api` to internal ingress in `environment.bicep`, drop the `eventsApiUrl` outputs, and have `deploy.yml` write the backend URL as `eventsBaseUrl`; verify `az bicep build` succeeds, the Aspire tests pass and `actionlint` is clean
- [x] 7.3 Update the AppHost comments, `src/Frontend/README.md` (events section) and `infra/README.md` (events-api row, route table description) so they describe the stream going through the gateway and the backend route; verify no document still says the browser opens the stream on the Events API directly

## 6. Integration

- [ ] 6.1 Build and test the solution from the worktree (`dotnet build src/Battleship.slnx`, `dotnet test` per test project as in `ci.yml`) and `az bicep build --file infra/main.bicep`; verify all green
- [ ] 6.2 After the pull request is merged and deployed, verify in the browser that `https://battleship.hexmaster.nl` loads over HTTPS, a game can be created and joined (Users and Games calls through `backend.<env>` succeed with their preflights), the event stream connects, and the `gateway` Container App no longer exists in the resource group; record the result on the issue
