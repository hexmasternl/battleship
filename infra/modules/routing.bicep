// The public backend endpoint: an HTTP route configuration on the Container Apps environment, built
// from infra/gateway-routes.json. That file mirrors GatewayRoutes in the AppHost (a test keeps them in
// step, and the container-apps-route-sync skill regenerates and compares it), so production routes
// exactly the prefixes the local YARP gateway does, each to its module's internal Container App, with
// the path unchanged: /users/me reaches users-api as /users/me.
param environmentName string

@description('Name of the route configuration; the first label of its address under the environment domain.')
param name string = 'backend'

var routes = loadJsonContent('../gateway-routes.json')

resource environment 'Microsoft.App/managedEnvironments@2024-10-02-preview' existing = {
  name: environmentName
}

// One rule per module, one route per prefix. pathSeparatedPrefix matches whole segments only, so
// /usersx never reaches users-api, and no prefixRewrite keeps the path as the API expects it.
// Prefixes are single, unique segments (GatewayRoutes.Validate), so rule order cannot shadow a rule.
resource routeConfig 'Microsoft.App/managedEnvironments/httpRouteConfigs@2024-10-02-preview' = {
  parent: environment
  name: name
  properties: {
    rules: map(routes.modules, module => {
      description: '${module.module} module'
      routes: map(module.prefixes, prefix => {
        match: {
          pathSeparatedPrefix: '/${prefix}'
          caseSensitive: true
        }
      })
      targets: [
        {
          containerApp: module.app
        }
      ]
    })
  }
}

output backendUrl string = 'https://${routeConfig.properties.fqdn}'
