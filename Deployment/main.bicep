param location string = 'AustraliaSouth'
param functionAppName string
param storageAccountName string
param appServicePlanName string
param tenantId string
param subscriptionId string
param appName string = 'SanxPublicHolidays'
param appInsightsName string = 'SanxPublicHolidaysInsights'

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
          value: 'holidaydata'
        }
        {
          name: 'ClientId'
          value: appRegistration.properties.appId
        }
        {
          name: 'ClientSecret'
          value: clientSecret.properties.secretText
        }
        {
          name: 'TenantId'
          value: tenantId
        }
      ]
    }
  }
  identity: {
    type: 'SystemAssigned'
  }
}

resource appRegistration 'Microsoft.Graph/applications@1.0' = {
  name: appName
  properties: {
    displayName: appName
    requiredResourceAccess: [
      {
        resourceAppId: '00000003-0000-0000-c000-000000000000' // Microsoft Graph
        resourceAccess: [
          {
            id: 'b340eb25-3456-403f-be2f-af7a0d370277' // Calendars.ReadWrite
            type: 'Scope'
          }
          {
            id: '5b567255-7703-4780-807c-7be8301ae99b' // MailboxSettings.Read
            type: 'Scope'
          }
          {
            id: 'a154be20-db9c-4678-8ab7-66f6cc099a59' // User.Read.All
            type: 'Scope'
          }
        ]
      }
    ]
  }
}

resource servicePrincipal 'Microsoft.Graph/servicePrincipals@1.0' = {
  name: appRegistration.properties.appId
  properties: {
    appId: appRegistration.properties.appId
  }
}

resource clientSecret 'Microsoft.Graph/applications/credentials@1.0' = {
  name: '${appRegistration.name}/clientSecret'
  properties: {
    secretText: 'YOUR-CLIENT-SECRET'
    displayName: 'Default'
  }
}

output functionAppName string = functionApp.name
output storageAccountName string = storageAccount.name
output appInsightsName string = appInsights.name
output appRegistrationId string = appRegistration.properties.appId
output clientSecret string = clientSecret.properties.secretText