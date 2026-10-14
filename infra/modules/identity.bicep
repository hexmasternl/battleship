// The identity the APIs and their Dapr sidecars use for table storage and Service Bus, so no
// account key or connection string is stored anywhere.
param location string
param suffix string

resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-battleship-${suffix}'
  location: location
}

output id string = identity.id
output clientId string = identity.properties.clientId
output principalId string = identity.properties.principalId
