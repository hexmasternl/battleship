// Service Bus namespace behind the Dapr pub/sub component. Topics need the Standard tier. Dapr creates
// each subscriber's subscription itself (consumer id = app id), which needs the Data Owner role.
param location string
param uniqueSuffix string
param principalId string

@description('Resource id of the identity; keeps the role assignment name known before deployment.')
param identityId string
param topics array

// Azure Service Bus Data Owner
var dataOwnerRoleId = '090c5cfd-751d-490a-894a-3ce6f1109419'

resource namespace 'Microsoft.ServiceBus/namespaces@2024-01-01' = {
  name: 'sb-battleship-${uniqueSuffix}'
  location: location
  sku: {
    name: 'Standard'
    tier: 'Standard'
  }
  properties: {
    minimumTlsVersion: '1.2'
    disableLocalAuth: true
  }
}

resource topic 'Microsoft.ServiceBus/namespaces/topics@2024-01-01' = [for name in topics: {
  parent: namespace
  name: name
}]

resource dataOwner 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(namespace.id, identityId, dataOwnerRoleId)
  scope: namespace
  properties: {
    principalId: principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', dataOwnerRoleId)
  }
}

output namespaceHost string = '${namespace.name}.servicebus.windows.net'
