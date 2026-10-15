// Production environment of Battleship. Deployed at subscription scope by .github/workflows/deploy.yml:
// creates the resource group and everything in it. The container registry is not managed here.
targetScope = 'subscription'

@description('Resource group that holds the environment. Created when missing.')
param resourceGroupName string = 'battleship-production'

@description('Azure region of the resource group and its resources.')
param location string = 'westeurope'

@description('Region of the Static Web App, which is only offered in a few regions.')
param staticWebAppLocation string = 'westeurope'

@description('Custom hostname of the frontend. Its CNAME record must point at the Static Web App before the deployment validates it; the APIs allow browsers from this origin only.')
param frontendHostname string = 'battleship.hexmaster.nl'

@description('Semantic version of the release; the tag of every API image.')
param version string

@description('Login server of the existing container registry, for example myregistry.azurecr.io.')
param acrLoginServer string

@description('User name for pulling from the container registry.')
param acrUsername string

@secure()
@description('Password for pulling from the container registry.')
param acrPassword string

@secure()
@description('Key the Users API signs access tokens with and every API validates them with.')
param signingKey string

@description('Service Bus topics the services publish to. Add a topic here together with the code that publishes it.')
param topics array = [
  'integration-events'
]

@description('Schedule of the Dapr cron binding that triggers the daily game cleanup.')
param gameCleanupSchedule string = '@daily'

resource resourceGroup 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: resourceGroupName
  location: location
}

module environment 'modules/environment.bicep' = {
  name: 'battleship-environment'
  scope: resourceGroup
  params: {
    location: location
    staticWebAppLocation: staticWebAppLocation
    frontendHostname: frontendHostname
    version: version
    acrLoginServer: acrLoginServer
    acrUsername: acrUsername
    acrPassword: acrPassword
    signingKey: signingKey
    topics: topics
    gameCleanupSchedule: gameCleanupSchedule
  }
}

output resourceGroupName string = resourceGroup.name
output gatewayUrl string = environment.outputs.gatewayUrl
output eventsApiUrl string = environment.outputs.eventsApiUrl
output staticWebAppName string = environment.outputs.staticWebAppName
output staticWebAppHostname string = environment.outputs.staticWebAppHostname
