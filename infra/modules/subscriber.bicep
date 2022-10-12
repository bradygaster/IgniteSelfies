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

resource imagesContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2022-05-01' = {
  name: '${storage.name}/default/keys'
}

resource signalr 'Microsoft.SignalRService/signalR@2022-02-01' existing = {
  name: '${abbrs.signalRServiceSignalR}-${resourceToken}'
}

resource scalerapp 'Microsoft.App/containerApps@2022-03-01' existing = {
  name: '${name}scaler'
}

resource containerapp 'Microsoft.App/containerApps@2022-03-01' = {
  name: '${name}subscriber'
  location: location
  tags: union(tags,  {  'azd-service-name': '${name}subscriber' })
  properties: {
    managedEnvironmentId: env.id
    configuration: {
      activeRevisionsMode: 'Single'
      dapr: {
        appId: 'image-upload-subscriber'
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
        external: true
        targetPort: 80
      }
    }
    template: {
      containers: [
        {
          image: image
          name: '${name}subscriber'
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
              name: 'FrontEnd'
              value: 'https://${name}publisher.${env.properties.defaultDomain}'
            }
            {
               name: 'SiloPort'
               value: '11111'
            }
            {
               name: 'GatewayPort'
               value: '30000'
            }
            {
              name: 'AzureSignalRConnectionString'
              value: signalr.listKeys().primaryConnectionString
            }
          ]
        }
      ]

      // scale: {
      //   minReplicas: 1
      //   maxReplicas: 1
      // }

      scale: {
        minReplicas: 1
        maxReplicas: 10
        rules: [
          {
            name: 'scaler'
            custom: {
              type: 'external'
              metadata: {
                scalerAddress: '${scalerapp.properties.configuration.ingress.fqdn}:80'
                graintype: 'selfieuser'
                siloNameFilter: 'silo'
                upperbound: '10'
              }
            }
          }
        ]
      }

    }
  }
}
