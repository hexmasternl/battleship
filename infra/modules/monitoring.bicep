// Log Analytics workspace for the Container Apps environment and workspace-based Application Insights
// for the APIs and their Dapr sidecars.
param location string
param suffix string

resource workspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: 'log-battleship-${suffix}'
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: 'appi-battleship-${suffix}'
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: workspace.id
  }
}

output workspaceName string = workspace.name
output appInsightsName string = appInsights.name
