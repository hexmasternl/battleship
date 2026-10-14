// Container Apps environment with the Dapr components the services use locally, under the same
// names: `pubsub` (here on Service Bus instead of RabbitMQ) and the `game-cleanup` cron binding.
param location string
param suffix string
param workspaceName string
param appInsightsName string
param serviceBusHost string
param identityClientId string
param gameCleanupSchedule string

resource workspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' existing = {
  name: workspaceName
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' existing = {
  name: appInsightsName
}

resource environment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: 'cae-battleship-${suffix}'
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: workspace.properties.customerId
        sharedKey: workspace.listKeys().primarySharedKey
      }
    }
    // Sidecar spans join the APIs' traces in Application Insights.
    daprAIConnectionString: appInsights.properties.ConnectionString
  }
}

// Same name and topic as AspireConstants.PubSubName / IntegrationEventsTopic. Scoped to the APIs that
// publish or subscribe; each sidecar authenticates with the shared managed identity.
resource pubsub 'Microsoft.App/managedEnvironments/daprComponents@2024-03-01' = {
  parent: environment
  name: 'pubsub'
  properties: {
    componentType: 'pubsub.azure.servicebus.topics'
    version: 'v1'
    metadata: [
      {
        name: 'namespaceName'
        value: serviceBusHost
      }
      {
        name: 'azureClientId'
        value: identityClientId
      }
    ]
    scopes: [
      'games-api'
      'users-api'
      'events-api'
    ]
  }
}

// The daily cleanup of inactive games: posts to the Games API's internal maintenance route, which the
// gateway never routes.
resource gameCleanup 'Microsoft.App/managedEnvironments/daprComponents@2024-03-01' = {
  parent: environment
  name: 'game-cleanup'
  properties: {
    componentType: 'bindings.cron'
    version: 'v1'
    metadata: [
      {
        name: 'schedule'
        value: gameCleanupSchedule
      }
      {
        name: 'route'
        value: '/maintenance/game-cleanup'
      }
      {
        name: 'direction'
        value: 'input'
      }
    ]
    scopes: [
      'games-api'
    ]
  }
}

output id string = environment.id
