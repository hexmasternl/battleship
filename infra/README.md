# Infrastructure

Bicep templates for the production environment of Battleship in Azure. `.github/workflows/deploy.yml` deploys them on every push to `main`. The deployment runs at subscription scope: it creates the resource group when it is missing and creates or updates everything in it.

The container registry is not managed here. The images come from the existing registry named by the `ACR_LOGIN_SERVER` secret.

## Resources

All resources live in the resource group `battleship-production` in `westeurope`.

| Resource | Name | Purpose |
|---|---|---|
| Log Analytics workspace | `log-battleship-production` | Logs of the Container Apps environment; backs Application Insights |
| Application Insights | `appi-battleship-production` | Traces, metrics and logs of the APIs and their Dapr sidecars |
| User-assigned managed identity | `id-battleship-production` | How the APIs and sidecars reach table storage and Service Bus. No keys are used |
| Storage account | `stbattleship<unique>` | Tables of the Users and Games APIs. Shared-key access is disabled |
| Service Bus namespace (Standard) | `sb-battleship-<unique>` | Topic `integration-events` behind the Dapr `pubsub` component |
| Container Apps environment | `cae-battleship-production` | Holds the Dapr components `pubsub` (Service Bus topics) and `game-cleanup` (cron, daily) |
| HTTP route configuration | `backend` on the environment | The backend address of the frontend (`https://backend.<environment domain>`). Routes each prefix of `gateway-routes.json` (`/users`, `/games`, `/events`) to its module's Container App with the path unchanged, exactly as the local YARP gateway does. No other address reaches the APIs |
| Container App `games-api` | internal | `battleship/games-api:<version>`. One replica minimum, because the cleanup binding runs in its sidecar. Allows browsers from the frontend origin |
| Container App `users-api` | internal | `battleship/users-api:<version>`. Scales to zero. Allows browsers from the frontend origin |
| Container App `events-api` | internal | `battleship/events-api:<version>`. Exactly one replica: stream routing state is in memory. Serves the server-sent event stream through the route configuration; its 15-second heartbeat keeps the stream below the ingress idle timeout. Allows the stream from the frontend origin |
| Static Web App (Free) | `stapp-battleship-production` | Serves the Angular frontend on `battleship.hexmaster.nl` (custom domain with a managed certificate) |

There is no gateway Container App. Earlier releases ran one; the deploy workflow deletes it when it still exists.

## Files

- `main.bicep` is the subscription-scope entry point. It creates the resource group and calls `modules/environment.bicep`.
- `main.bicepparam` holds the non-secret parameter values.
- `gateway-routes.json` lists the backend routes: per module its Container App and its prefixes. It must list the same modules and prefixes as `GatewayRoutes` in the AppHost, and `GatewayRoutesFileTests` fails when they differ. The `container-apps-route-sync` skill regenerates it from the AppHost table and compares it with the deployed route configuration; the `yarp-gateway-routes` skill proposes and approves every prefix.
- `modules/routing.bicep` turns `gateway-routes.json` into the environment's HTTP route configuration: one rule per module, one path-segment prefix match per prefix, no rewrite.
- `modules/` otherwise contains one file per concern. `containerapp.bicep` is shared by the three APIs.

## Parameters

| Parameter | Source | Notes |
|---|---|---|
| `resourceGroupName` | `main.bicepparam` | Default `battleship-production` |
| `location` | `main.bicepparam` | Default `westeurope` |
| `staticWebAppLocation` | `main.bicepparam` | Default `westeurope`. Static Web Apps exist only in a few regions |
| `frontendHostname` | `main.bicepparam` | Default `battleship.hexmaster.nl`. The custom hostname of the Static Web App and the only origin the APIs allow browsers from. Its CNAME record must exist before the deployment (see Azure setup) |
| `topics` | `main.bicepparam` | Default `integration-events`. Add a topic here together with the code that publishes to it |
| `gameCleanupSchedule` | `main.bicepparam` | Default `@daily` |
| `version` | workflow | The release version computed by GitVersion; the tag of every image |
| `acrLoginServer`, `acrUsername` | workflow, from `ACR_LOGIN_SERVER` and `ACR_LOGIN_USERNAME` | |
| `acrPassword` | workflow, from `ACR_LOGIN_PASSWORD` | Secure. Stored as a Container Apps secret |
| `signingKey` | workflow, from `USERS_SIGNING_KEY` | Secure. Stored as a Container Apps secret and read as `Users__Tokens__SigningKey` |

## GitHub setup

**Repository secrets**
- `ACR_LOGIN_SERVER`
- `ACR_LOGIN_USERNAME`
- `ACR_LOGIN_PASSWORD`

**Secrets of the `production` environment**
- `AZURE_CLIENT_ID`, `AZURE_TENANT_ID` and `AZURE_SUBSCRIPTION_ID`: the Microsoft Entra app the workflow signs in as, through OIDC. No client secret is involved.
- `USERS_SIGNING_KEY`: the key access tokens are signed with.
  - It must be at least 48 characters, letters and digits only.
  - Keep it stable: changing it signs every player out.
  - Generate one with:
    ```bash
    openssl rand -base64 64 | tr -dc 'A-Za-z0-9' | head -c 64
    ```
  - The deploy job stops with an error when it is missing.

## Azure setup

Before the first deployment:

1. **Add a federated credential** to the Entra app behind `AZURE_CLIENT_ID`:
   - issuer `https://token.actions.githubusercontent.com`
   - subject `repo:hexmasternl/battleship:environment:production`
   - audience `api://AzureADTokenExchange`
2. **Grant its service principal subscription roles.** It creates the resource group and assigns roles to the managed identity, so it needs one of:
   - **Owner**
   - **Contributor** plus **Role Based Access Control Administrator**
3. **Register the resource providers:** `Microsoft.App`, `Microsoft.ServiceBus`, `Microsoft.Storage`, `Microsoft.Web`, `Microsoft.OperationalInsights`, `Microsoft.Insights` and `Microsoft.ManagedIdentity`. Registration is a one-time step per subscription, for example `az provider register --namespace Microsoft.App`.
4. **Create the DNS record for the frontend hostname.** The deployment binds `frontendHostname` (`battleship.hexmaster.nl`) to the Static Web App and validates it through a CNAME, so the record must exist first:
   - In the `hexmaster.nl` zone, add `CNAME battleship -> <default hostname of the Static Web App>`.
   - The default hostname (`stapp-battleship-production.<hash>.azurestaticapps.net`) is the `staticWebAppHostname` output of every deployment. On a fresh subscription it does not exist yet: run the deployment once, let it fail on the custom domain, read the output from the resource group's deployment history (or `az staticwebapp show --name stapp-battleship-production --resource-group battleship-production --query defaultHostname`), create the record and run the deployment again.
   - Azure issues the certificate after validation; it can take a few minutes before `https://battleship.hexmaster.nl` serves without a warning.

## Running it by hand

Preview what a deployment would change:

```bash
az deployment sub what-if --location westeurope \
  --template-file infra/main.bicep --parameters infra/main.bicepparam \
  --parameters version=<version> acrLoginServer=<server> acrUsername=<user> acrPassword=<password> signingKey=<key>
```

`az deployment sub create` with the same arguments deploys.

## Rollback

Every release's images stay in the registry, and its frontend artifact stays on its workflow run until the artifact retention period expires.

To roll back, open an earlier successful run of the Deploy workflow and re-run its `deploy` job. That redeploys the templates with that run's version, which moves every Container App back to those images, and uploads that run's frontend with the backend address of that deployment.

Releases do not migrate data, so rolling back is safe. Rolling back to a release that still ran the gateway Container App recreates it; the route configuration and the custom domain stay in place, unused, until the next release.

## Teardown

```bash
az group delete --name battleship-production
```

This removes the whole environment, including its tables. The container registry is not affected.
