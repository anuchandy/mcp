@description('Location for all resources')
param location string = resourceGroup().location

@description('Default name for Azure Container App, and name prefix for all other resources')
param name string

@description('Azure Container App name')
param containerAppName string = name

@description('Environment name for the Container Apps Environment')
param environmentName string = '${name}-env'

@description('Number of CPU cores allocated to the container')
param cpuCores string = '0.25'

@description('Amount of memory allocated to the container')
param memorySize string = '0.5Gi'

@description('Minimum number of replicas')
param minReplicas int = 1

@description('Maximum number of replicas')
param maxReplicas int = 3

@description('Application Insights connection string')
param appInsightsConnectionString string

@description('Whether to collect telemetry')
param azureMcpCollectTelemetry string

@description('Azure MCP Server namespaces to enable. Must specify at least one namespace and no more than three.')
@minLength(1)
@maxLength(3)
param namespaces array

var azureAdTenantId = '00000000-0000-0000-0000-000000000000'
var azureAdClientId = '00000000-0000-0000-0000-000000000000'

var baseArgs = [
  '--transport'
  'http'
  '--outgoing-auth-strategy'
  'UseHostingEnvironmentIdentity'
  '--mode'
  'all'
  '--read-only'
  '--dangerously-disable-http-incoming-auth'
]
var namespaceArgs = [for ns in namespaces: ['--namespace', ns]]
var serverArgs = flatten(concat([baseArgs], namespaceArgs))

resource containerAppsEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: environmentName
  location: location
  properties: {
  }
}

resource containerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: containerAppName
  location: location
  tags: {
    product: 'azmcp'
  }
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    managedEnvironmentId: containerAppsEnvironment.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: 8080
        allowInsecure: false
        transport: 'http'
        traffic: [
          {
            weight: 100
            latestRevision: true
          }
        ]
      }
    }
    template: {
      containers: [
        {
          image: 'mcr.microsoft.com/azure-sdk/azure-mcp:latest'
          name: containerAppName
          command: []
          args: serverArgs
          resources: {
            cpu: json(cpuCores)
            memory: memorySize
          }
          env: concat([
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: 'Production'
            }
            {
              name: 'ASPNETCORE_URLS'
              value: 'http://0.0.0.0:8080'
            }
            {
              name: 'AZURE_TOKEN_CREDENTIALS'
              value: 'managedidentitycredential'
            }
            {
              name: 'AZURE_MCP_INCLUDE_PRODUCTION_CREDENTIALS'
              value: 'true'
            }
            {
              name: 'AZURE_MCP_COLLECT_TELEMETRY'
              value: azureMcpCollectTelemetry
            }
            {
              name: 'ALLOW_INSECURE_EXTERNAL_BINDING'
              value: 'true'
            }
            // {
            //   name: 'AzureAd__Instance'
            //   value: environment().authentication.loginEndpoint
            // }
            // {
            //   name: 'AzureAd__TenantId'
            //   value: azureAdTenantId
            // }
            // {
            //   name: 'AzureAd__ClientId'
            //   value: azureAdClientId
            // }
            {
              name: 'AZURE_LOG_LEVEL'
              value: 'Verbose'
            }
          ], !empty(appInsightsConnectionString) ? [
            {
              name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
              value: appInsightsConnectionString
            }
          ] : [])
        }
      ]
      scale: {
        minReplicas: minReplicas
        maxReplicas: maxReplicas
        rules: [
          {
            name: 'http-scaler'
            http: {
              metadata: {
                concurrentRequests: '100'
              }
            }
          }
        ]
      }
    }
  }
}

output containerAppResourceId string = containerApp.id
output containerAppUrl string = 'https://${containerApp.properties.configuration.ingress.fqdn}'
output containerAppName string = containerApp.name
output containerAppPrincipalId string = containerApp.identity.principalId
output containerAppEnvironmentId string = containerAppsEnvironment.id

