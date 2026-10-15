using 'main.bicep'

param workloadName = 'msgproc'
param environmentName = 'dev'
param serviceBusSku = 'Standard'
param maximumInstanceCount = 10
param instanceMemoryMB = 2048

// Set via the pipeline (DEVELOPER_PRINCIPAL_ID variable) or leave empty
param developerPrincipalId = readEnvironmentVariable('DEVELOPER_PRINCIPAL_ID', '')

param tags = {
  owner: 'platform-team'
}
