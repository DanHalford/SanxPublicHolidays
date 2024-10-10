@description('Region resources are deployed to')
param location string = resourceGroup().location

@description('Name of the function app')
param functionAppName string = 'SanxHolidays'

@description('Name of the storage account')
@minLength(3)
@maxLength(24)
param storageAccountName string = 'sto${toLower(functionAppName)}'

@description('Name of the storage container used for holiday data files')
@minLength(3)
@maxLength(63)
param storageContainerName string = 'holiday-data'

@description('Name of the app service plan')
param appServicePlanName string = '${functionAppName}-plan'

@description('Name of the app insights instance')
param appInsightsName string = '${functionAppName}-insights'

@description('Your Entra ID tenant ID')
param tenantId string

@description('Your Entra ID application (client) ID')
param clientId string

@description('Client secret created for the app registration')
param clientSecret string

resource appServicePlan 'Microsoft.Web/serverfarms@2021-02-01' = {
  name: appServicePlanName
  location: location
  sku: {
    name: 'Y1'
    tier: 'Dynamic'
  }
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2021-04-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'Storage'
}

resource blobContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2021-04-01' = {
  name: '${storageAccountName}/${storageContainerName}'
  properties: {
    publicAccess: 'None'
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
  }
}

resource functionApp 'Microsoft.Web/sites@2021-02-01' = {
  name: functionAppName
  location: location
  kind: 'functionapp'
  properties: {
    serverFarmId: appServicePlan.id
    siteConfig: {
      appSettings: [
        {
          name: 'AzureWebJobsStorage'
          value: storageAccount.properties.primaryEndpoints.blob
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet'
        }
        {
          name: 'APPINSIGHTS_INSTRUMENTATIONKEY'
          value: appInsights.properties.InstrumentationKey
        }
        {
          name: 'HolidayDataConnectionString'
          value: storageAccount.properties.primaryEndpoints.blob
        }
        {
          name: 'HolidayDataContainerName'
          value: storageContainerName
        }
        {
          name: 'AZURE_TENANT_ID'
          value: tenantId
        }
        {
          name: 'AZURE_CLIENT_ID'
          value: clientId
        }
        {
          name: 'AZURE_CLIENT_SECRET'
          value: clientSecret
        }
      ]
    }
  }
  identity: {
    type: 'SystemAssigned'
  }
}

output functionAppName string = functionApp.name
output storageAccountName string = storageAccount.name
output appInsightsName string = appInsights.name
