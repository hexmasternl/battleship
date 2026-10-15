// The public backend endpoint: the same YARP container image the Aspire AppHost runs locally, configured
// through environment variables generated from infra/gateway-routes.json. That file mirrors
// GatewayRoutes in the AppHost (a test keeps them in step), so production routes exactly the prefixes
// local runs do, each to its module's internal Container App, with the path unchanged.
param location string
param environmentId string

@description('The single origin the browser calls from; the ingress answers CORS requests for it.')
param frontendOrigin string

// Pinned to the tag Aspire.Hosting.Yarp uses, so production matches local runs.
param image string = 'mcr.microsoft.com/dotnet/nightly/yarp:2.3-preview'

var routes = loadJsonContent('../gateway-routes.json')
var port = 5000

// One route per prefix: /{prefix}/{**catch-all} to the module's cluster.
var routeEnv = flatten(map(routes.modules, module => flatten(map(module.prefixes, prefix => [
  {
    name: 'ReverseProxy__Routes__${prefix}__ClusterId'
    value: module.app
  }
  {
    name: 'ReverseProxy__Routes__${prefix}__Match__Path'
    value: '/${prefix}/{**catch-all}'
  }
]))))

// One cluster per module: its Container App inside the environment.
var clusterEnv = map(routes.modules, module => {
  name: 'ReverseProxy__Clusters__${module.app}__Destinations__d1__Address'
  value: 'http://${module.app}'
})

// The deployment name must differ from the outer 'gateway' module in environment.bicep: ARM rejects a
// nested deployment that reuses its parent's name with DeploymentActive.
module gateway 'containerapp.bicep' = {
  name: 'gateway-containerapp'
  params: {
    name: 'gateway'
    location: location
    environmentId: environmentId
    image: image
    targetPort: port
    external: true
    minReplicas: 1
    maxReplicas: 3
    env: concat([
      {
        name: 'ASPNETCORE_URLS'
        value: 'http://+:${port}'
      }
    ], routeEnv, clusterEnv)
    corsAllowedOrigins: [
      frontendOrigin
    ]
  }
}

output url string = 'https://${gateway.outputs.fqdn}'
