// Non-secret values for the production deployment. The workflow adds version, the registry
// settings and the secure parameters (acrPassword, signingKey) on the command line.
using 'main.bicep'

param resourceGroupName = 'battleship-production'
param location = 'westeurope'
param staticWebAppLocation = 'westeurope'
param frontendHostname = 'battleship.hexmaster.nl'
param topics = [
  'integration-events'
]
param gameCleanupSchedule = '@daily'

// Supplied by the workflow.
param version = ''
param acrLoginServer = ''
param acrUsername = ''
param acrPassword = ''
param signingKey = ''
