// One Container App. Used for the APIs (registry image, Dapr sidecar, managed identity) and for the
// gateway (public image, no sidecar); the optional parts are left out when their parameters are empty.
param name string
param location string
param environmentId string
param image string
param targetPort int

@description('true for internet-facing ingress, false for ingress reachable only inside the environment.')
param external bool

param minReplicas int = 1
param maxReplicas int = 3

@description('Dapr app id; empty runs the app without a sidecar.')
param daprAppId string = ''

@description('Resource id of the user-assigned identity; empty runs the app without one.')
param identityId string = ''

@description('Registry login server; empty pulls a public image.')
param registryServer string = ''
param registryUsername string = ''

@description('Plain environment variables as { name, value } items.')
param env array = []

@secure()
@description('Secrets as name -> value. Each is stored as a Container Apps secret; reference them from secretEnv.')
param secrets object = {}

@description('Environment variables read from secrets, as { name, secretRef } items.')
param secretEnv array = []

@description('Origins the ingress answers CORS requests for; empty leaves CORS to the app.')
param corsAllowedOrigins array = []

var registryPasswordSecretName = 'registry-password'

var secretList = [for secret in items(secrets): {
  name: secret.key
  value: secret.value
}]

resource app 'Microsoft.App/containerApps@2024-03-01' = {
  name: name
  location: location
  identity: empty(identityId) ? null : {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${identityId}': {}
    }
  }
  properties: {
    managedEnvironmentId: environmentId
    configuration: {
      activeRevisionsMode: 'Single'
      secrets: secretList
      registries: empty(registryServer) ? [] : [
        {
          server: registryServer
          username: registryUsername
          passwordSecretRef: registryPasswordSecretName
        }
      ]
      ingress: {
        external: external
        targetPort: targetPort
        transport: 'http'
        allowInsecure: !external
        corsPolicy: empty(corsAllowedOrigins) ? null : {
          allowedOrigins: corsAllowedOrigins
          allowedMethods: [
            '*'
          ]
          allowedHeaders: [
            '*'
          ]
          maxAge: 600
        }
      }
      dapr: empty(daprAppId) ? null : {
        enabled: true
        appId: daprAppId
        appPort: targetPort
        appProtocol: 'http'
      }
    }
    template: {
      containers: [
        {
          name: name
          image: image
          env: concat(env, secretEnv)
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
        }
      ]
      scale: {
        minReplicas: minReplicas
        maxReplicas: maxReplicas
      }
    }
  }
}

output fqdn string = app.properties.configuration.ingress.fqdn
