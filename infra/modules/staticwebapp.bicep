// Hosts the Angular frontend. Not linked to the repository: the workflow uploads the built artifact.
param location string
param suffix string

@description('Custom hostname of the site. A CNAME from it to the default hostname must exist before the deployment, or the validation fails.')
param hostname string

resource site 'Microsoft.Web/staticSites@2023-12-01' = {
  name: 'stapp-battleship-${suffix}'
  location: location
  sku: {
    name: 'Free'
    tier: 'Free'
  }
  properties: {}
}

// Validated through the CNAME; Azure issues and renews the certificate itself.
resource customDomain 'Microsoft.Web/staticSites/customDomains@2023-12-01' = {
  parent: site
  name: hostname
  properties: {
    validationMethod: 'cname-delegation'
  }
}

output name string = site.name
output defaultHostname string = site.properties.defaultHostname
