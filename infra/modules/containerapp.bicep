// One Container App for an API: registry image, Dapr sidecar and managed identity. The optional parts
// are left out when their parameters are empty.
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
