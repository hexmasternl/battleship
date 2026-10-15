// Everything inside the resource group: shared resources, the Container Apps environment with its
// route configuration, the APIs and the Static Web App. Container App names equal their Dapr app ids,
// as locally.
param location string
param staticWebAppLocation string

@description('Custom hostname the frontend is served on; the only origin the APIs allow browsers from.')
param frontendHostname string

param version string
param acrLoginServer string
param acrUsername string

@secure()
param acrPassword string

@secure()
param signingKey string

param topics array
param gameCleanupSchedule string

var suffix = 'production'
var uniqueSuffix = uniqueString(resourceGroup().id)

// Known before deployment (unlike module outputs), so role assignment names can be derived from it.
var identityResourceId = resourceId('Microsoft.ManagedIdentity/userAssignedIdentities', 'id-battleship-${suffix}')

module monitoring 'monitoring.bicep' = {
  name: 'monitoring'
  params: {
    location: location
    suffix: suffix
  }
}

module identity 'identity.bicep' = {
  name: 'identity'
  params: {
    location: location
    suffix: suffix
  }
}

module storage 'storage.bicep' = {
  name: 'storage'
  params: {
    location: location
    uniqueSuffix: uniqueSuffix
    principalId: identity.outputs.principalId
    identityId: identityResourceId
  }
}

module serviceBus 'servicebus.bicep' = {
  name: 'servicebus'
  params: {
    location: location
    uniqueSuffix: uniqueSuffix
    principalId: identity.outputs.principalId
    identityId: identityResourceId
    topics: topics
  }
}

module staticWebApp 'staticwebapp.bicep' = {
  name: 'staticwebapp'
  params: {
    location: staticWebAppLocation
    suffix: suffix
    hostname: frontendHostname
  }
}

module containerAppsEnvironment 'containerapps-env.bicep' = {
  name: 'containerapps-env'
  params: {
    location: location
    suffix: suffix
    workspaceName: monitoring.outputs.workspaceName
    appInsightsName: monitoring.outputs.appInsightsName
    serviceBusHost: serviceBus.outputs.namespaceHost
    identityClientId: identity.outputs.clientId
    gameCleanupSchedule: gameCleanupSchedule
  }
}


// The browser loads the app from here; the APIs allow cross-origin requests from this origin only.
var frontendOrigin = 'https://${frontendHostname}'

// Settings every API shares: the managed identity for the Azure SDK and the telemetry destination.
var apiEnv = [
  {
    name: 'AZURE_CLIENT_ID'
    value: identity.outputs.clientId
  }
  {
    name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
    value: monitoring.outputs.appInsightsConnectionString
  }
]

// Table storage through the managed identity: a URI instead of a connection string with a key.
var tablesEnv = [
  {
    name: 'ConnectionStrings__tables'
    value: storage.outputs.tableEndpoint
  }
]

// The Users and Games APIs answer the browser's CORS requests themselves (GatewayCorsExtensions in
// ServiceDefaults), for the frontend origin only.
var gatewayCorsEnv = [
  {
    name: 'Gateway__AllowedOrigins'
    value: frontendOrigin
  }
]

var signingKeyEnv = [
  {
    name: 'Users__Tokens__SigningKey'
    secretRef: 'users-signing-key'
  }
]

var registrySecrets = {
  'registry-password': acrPassword
}

var apiSecrets = union(registrySecrets, {
  'users-signing-key': signingKey
})

// Internal: reached only through the environment's route configuration. Stays at one replica, because
// the game-cleanup cron binding lives in its sidecar.
module gamesApi 'containerapp.bicep' = {
  name: 'games-api'
  params: {
    name: 'games-api'
    location: location
    environmentId: containerAppsEnvironment.outputs.id
    image: '${acrLoginServer}/battleship/games-api:${version}'
    targetPort: 8080
    external: false
    minReplicas: 1
    maxReplicas: 3
    daprAppId: 'games-api'
    identityId: identity.outputs.id
    registryServer: acrLoginServer
    registryUsername: acrUsername
    env: concat(apiEnv, tablesEnv, gatewayCorsEnv)
    secrets: apiSecrets
    secretEnv: signingKeyEnv
  }
}

// Internal: reached only through the environment's route configuration. Keeps one warm replica, so the
// first player after a quiet period does not wait for a cold start.
module usersApi 'containerapp.bicep' = {
  name: 'users-api'
  params: {
    name: 'users-api'
    location: location
    environmentId: containerAppsEnvironment.outputs.id
    image: '${acrLoginServer}/battleship/users-api:${version}'
    targetPort: 8080
    external: false
    minReplicas: 1
    maxReplicas: 3
    daprAppId: 'users-api'
    identityId: identity.outputs.id
    registryServer: acrLoginServer
    registryUsername: acrUsername
    env: concat(apiEnv, tablesEnv, gatewayCorsEnv)
    secrets: apiSecrets
    secretEnv: signingKeyEnv
  }
}

// Internal: the browser opens the event stream through the environment's route configuration
// (/events), which forwards the frontend origin unchanged, so the stream's own CORS policy allows it.
// Exactly one replica, because the routing state of open streams lives in memory.
module eventsApi 'containerapp.bicep' = {
  name: 'events-api'
  params: {
    name: 'events-api'
    location: location
    environmentId: containerAppsEnvironment.outputs.id
    image: '${acrLoginServer}/battleship/events-api:${version}'
    targetPort: 8080
    external: false
    minReplicas: 1
    maxReplicas: 1
    daprAppId: 'events-api'
    identityId: identity.outputs.id
    registryServer: acrLoginServer
    registryUsername: acrUsername
    env: concat(apiEnv, [
      {
        name: 'Events__AllowedOrigins__0'
        value: frontendOrigin
      }
    ])
    secrets: registrySecrets
  }
}

// The frontend's only backend URL: the environment routes each prefix of gateway-routes.json to the
// module's internal Container App. The apps must exist before the rules can target them.
module routing 'routing.bicep' = {
  name: 'routing'
  params: {
    environmentName: containerAppsEnvironment.outputs.name
  }
  dependsOn: [
    gamesApi
    usersApi
    eventsApi
  ]
}

output backendUrl string = routing.outputs.backendUrl
output staticWebAppName string = staticWebApp.outputs.name
output staticWebAppHostname string = staticWebApp.outputs.defaultHostname
