param location string
param namespaceName string
param sku string
param queueName string
param tags object

@description('Function app identity that receives (and completes / dead-letters) messages.')
param receiverPrincipalId string

@description('Optional user/group object ID granted send + receive for local testing.')
param developerPrincipalId string = ''

var dataReceiverRoleId = '4f6d3b9b-027b-4f4c-9142-0e5a2a2247e0'
var dataSenderRoleId = '69a216fc-b8fb-44d8-bc22-1f3c2552a2a8'

resource namespace 'Microsoft.ServiceBus/namespaces@2024-01-01' = {
  name: namespaceName
  location: location
  tags: tags
  sku: {
    name: sku
    tier: sku
  }
  properties: {
    minimumTlsVersion: '1.2'
    disableLocalAuth: true // Entra ID only; SAS keys disabled
  }
}

resource queue 'Microsoft.ServiceBus/namespaces/queues@2024-01-01' = {
  parent: namespace
  name: queueName
  properties: {
    maxDeliveryCount: 10
    lockDuration: 'PT1M'
    defaultMessageTimeToLive: 'P14D'
    deadLetteringOnMessageExpiration: true
    requiresDuplicateDetection: false
  }
}

resource receiverRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(queue.id, receiverPrincipalId, dataReceiverRoleId)
  scope: queue
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', dataReceiverRoleId)
    principalId: receiverPrincipalId
    principalType: 'ServicePrincipal'
  }
}

resource developerRoles 'Microsoft.Authorization/roleAssignments@2022-04-01' = [for roleId in (empty(developerPrincipalId) ? [] : [ dataReceiverRoleId, dataSenderRoleId ]): {
  name: guid(queue.id, developerPrincipalId, roleId)
  scope: queue
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleId)
    principalId: developerPrincipalId
  }
}]

output name string = namespace.name
output fullyQualifiedNamespace string = '${namespace.name}.servicebus.windows.net'
