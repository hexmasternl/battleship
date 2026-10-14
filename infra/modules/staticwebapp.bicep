// Hosts the Angular frontend. Not linked to the repository: the workflow uploads the built artifact.
param location string
param suffix string

resource site 'Microsoft.Web/staticSites@2023-12-01' = {
  name: 'stapp-battleship-${suffix}'
  location: location
  sku: {
    name: 'Free'
    tier: 'Free'
  }
  properties: {}
}

output name string = site.name
output defaultHostname string = site.properties.defaultHostname
