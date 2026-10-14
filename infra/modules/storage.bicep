// Table storage for the Users and Games APIs (users, refresh tokens, games). Access is through the
// managed identity only; shared keys are disabled.
param location string
param uniqueSuffix string
param principalId string

// Storage Table Data Contributor
var tableDataContributorRoleId = '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3'

resource account 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: take('stbattleship${uniqueSuffix}', 24)
  location: location
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    allowBlobPublicAccess: false
    allowSharedKeyAccess: false
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
  }
}

resource tableDataContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(account.id, principalId, tableDataContributorRoleId)
  scope: account
  properties: {
    principalId: principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', tableDataContributorRoleId)
  }
}

output tableEndpoint string = account.properties.primaryEndpoints.table
