param location string
param planName string
param functionAppName string
param tags object

param identityId string
param identityClientId string

param storageAccountName string
param deploymentContainerUrl string
param appInsightsConnectionString string
param serviceBusFqdn string
param queueName string
param dataverseUrl string
param maximumInstanceCount int
param instanceMemoryMB int

resource plan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: planName
  location: location
  tags: tags
  kind: 'functionapp'
  sku: {
    tier: 'FlexConsumption'
    name: 'FC1'
  }
  properties: {
    reserved: true // Linux
  }
}

resource functionApp 'Microsoft.Web/sites@2024-04-01' = {
  name: functionAppName
  location: location
  tags: tags
  kind: 'functionapp,linux'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${identityId}': {}
    }
  }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: {
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      appSettings: [
        // Host storage, identity-based (no keys)
        { name: 'AzureWebJobsStorage__accountName', value: storageAccountName }
        { name: 'AzureWebJobsStorage__credential', value: 'managedidentity' }
        { name: 'AzureWebJobsStorage__clientId', value: identityClientId }

        // Telemetry, Entra ID auth (local auth is disabled on App Insights)
        { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsightsConnectionString }
        { name: 'APPLICATIONINSIGHTS_AUTHENTICATION_STRING', value: 'ClientId=${identityClientId};Authorization=AAD' }

        // Service Bus trigger connection, identity-based. Prefix matches Connection = "ServiceBusConnection".
        { name: 'ServiceBusConnection__fullyQualifiedNamespace', value: serviceBusFqdn }
        { name: 'ServiceBusConnection__credential', value: 'managedidentity' }
        { name: 'ServiceBusConnection__clientId', value: identityClientId }
        { name: 'ServiceBusQueueName', value: queueName }
        { name: 'Dataverse__Url', value: dataverseUrl }
      ]
    }
    functionAppConfig: {
      deployment: {
        storage: {
          type: 'blobContainer'
          value: deploymentContainerUrl
          authentication: {
            type: 'UserAssignedIdentity'
            userAssignedIdentityResourceId: identityId
          }
        }
      }
      scaleAndConcurrency: {
        maximumInstanceCount: maximumInstanceCount
        instanceMemoryMB: instanceMemoryMB
      }
      runtime: {
        name: 'dotnet-isolated'
        version: '10.0'
      }
    }
  }
}

output name string = functionApp.name
output defaultHostName string = functionApp.properties.defaultHostName
