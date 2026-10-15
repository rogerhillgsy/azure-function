targetScope = 'resourceGroup'

@description('Short workload name used in resource names.')
@minLength(2)
@maxLength(10)
param workloadName string

@description('Environment name, e.g. dev, test, prod.')
@allowed([ 'dev', 'test', 'prod' ])
param environmentName string

param location string = resourceGroup().location

@description('Name of the queue the function listens to.')
param queueName string = 'orders'

@description('Service Bus SKU. Standard is needed for topics; Premium for VNet integration / large messages.')
@allowed([ 'Basic', 'Standard', 'Premium' ])
param serviceBusSku string = 'Standard'

@description('Upper bound on Flex Consumption instances.')
@minValue(1)
@maxValue(1000)
param maximumInstanceCount int = 40

@description('Memory per Flex Consumption instance (MB).')
@allowed([ 512, 2048, 4096 ])
param instanceMemoryMB int = 2048

@description('Optional object ID of a user or group to grant Service Bus send/receive (handy for local debugging). Leave empty to skip.')
param developerPrincipalId string = ''

param tags object = {}

var suffix = uniqueString(resourceGroup().id, workloadName, environmentName)
var baseName = toLower('${workloadName}-${environmentName}')
var allTags = union(tags, {
  workload: workloadName
  environment: environmentName
})

var names = {
  identity: 'id-${baseName}'
  logAnalytics: 'log-${baseName}'
  appInsights: 'appi-${baseName}'
  storage: take(toLower('st${replace(workloadName, '-', '')}${environmentName}${suffix}'), 24)
  serviceBus: 'sb-${baseName}-${suffix}'
  plan: 'asp-${baseName}'
  functionApp: 'func-${baseName}-${suffix}'
}

var deploymentContainerName = 'app-package-${take(names.functionApp, 32)}'

resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: names.identity
  location: location
  tags: allTags
}

module monitoring 'modules/monitoring.bicep' = {
  name: 'monitoring'
  params: {
    location: location
    logAnalyticsName: names.logAnalytics
    appInsightsName: names.appInsights
    tags: allTags
    publisherPrincipalId: identity.properties.principalId
  }
}

module storage 'modules/storage.bicep' = {
  name: 'storage'
  params: {
    location: location
    storageAccountName: names.storage
    deploymentContainerName: deploymentContainerName
    tags: allTags
    functionPrincipalId: identity.properties.principalId
  }
}

module serviceBus 'modules/servicebus.bicep' = {
  name: 'servicebus'
  params: {
    location: location
    namespaceName: names.serviceBus
    sku: serviceBusSku
    queueName: queueName
    tags: allTags
    receiverPrincipalId: identity.properties.principalId
    developerPrincipalId: developerPrincipalId
  }
}

module functionApp 'modules/functionapp.bicep' = {
  name: 'functionapp'
  params: {
    location: location
    planName: names.plan
    functionAppName: names.functionApp
    tags: allTags
    identityId: identity.id
    identityClientId: identity.properties.clientId
    storageAccountName: storage.outputs.name
    deploymentContainerUrl: '${storage.outputs.blobEndpoint}${deploymentContainerName}'
    appInsightsConnectionString: monitoring.outputs.connectionString
    serviceBusFqdn: serviceBus.outputs.fullyQualifiedNamespace
    queueName: queueName
    maximumInstanceCount: maximumInstanceCount
    instanceMemoryMB: instanceMemoryMB
  }
  // The outputs consumed above create implicit dependencies on the storage, Service Bus and monitoring
  // modules, so their role assignments exist before the function host first starts.
}

output functionAppName string = functionApp.outputs.name
output functionAppHostName string = functionApp.outputs.defaultHostName
output serviceBusNamespace string = serviceBus.outputs.fullyQualifiedNamespace
output queueName string = queueName
