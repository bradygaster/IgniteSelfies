param name string
param image string
param location string

var resourceToken = toLower(uniqueString(subscription().id, name, location))
var tags = { 'azd-env-name': name }
var abbrs = loadJsonContent('../abbreviations.json')

resource acr 'Microsoft.ContainerRegistry/registries@2022-02-01-preview' existing = {
  name: '${abbrs.containerRegistryRegistries}${resourceToken}'
}

resource ai 'Microsoft.Insights/components@2020-02-02' existing = {
  name: '${abbrs.insightsComponents}${resourceToken}'
}

resource env 'Microsoft.App/managedEnvironments@2022-03-01' existing = {
  name: '${abbrs.appManagedEnvironments}${resourceToken}'
}

resource storage 'Microsoft.Storage/storageAccounts@2021-09-01' existing = {
  name: '${abbrs.storageStorageAccounts}${resourceToken}'
}

resource scalerapp 'Microsoft.App/containerApps@2022-03-01' = {
  name: '${name}scaler'
  location: location
  tags: union(tags,  {  'azd-service-name': '${name}subscriber' })
  properties: {
    managedEnvironmentId: env.id
    configuration: {
      activeRevisionsMode: 'Single'
      dapr: {
        appId: 'scaler'
        appPort: 80
        appProtocol: 'http'
        enabled: true
      }
      secrets: [
        {
          name: 'container-registry-password'
          value: acr.listCredentials().passwords[0].value
        }
      ]
      registries: [
        {
          server: '${acr.name}.azurecr.io'
          username: acr.name
          passwordSecretRef: 'container-registry-password'
        }
      ]
      ingress: {
        external: false
        targetPort: 80
        allowInsecure: true
        transport: 'http2'
      }
    }
    template: {
      containers: [
        {
          image: image
          name: '${name}scaler'
          env: [
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: 'Development'
            }
            {
              name: 'AzureStorageConnectionString'
              value: 'DefaultEndpointsProtocol=https;AccountName=${storage.name};AccountKey=${storage.listKeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}'
            }
            {
              name: 'ASPNETCORE_LOGGING__CONSOLE__DISABLECOLORS'
              value: 'true'
            }
            {
              name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
              value: ai.properties.ConnectionString
            }
            {
              name: 'APPLICATIONINSIGHTS_INSTRUMENTATIONKEY'
              value: ai.properties.InstrumentationKey
            }
            {
               name: 'SiloPort'
               value: '11111'
            }
            {
               name: 'GatewayPort'
               value: '30000'
            }
          ]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 1
      }
    }
  }
}

