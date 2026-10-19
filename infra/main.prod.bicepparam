using 'main.bicep'

param workloadName = 'msgproc'
param environmentName = 'prod'
param serviceBusSku = 'Standard'
param maximumInstanceCount = 100
param instanceMemoryMB = 2048
param dataverseUrl = readEnvironmentVariable('DATAVERSE_URL', '')

param tags = {
  owner: 'platform-team'
}
