param name string
param image string
param location string
param ingress bool = true

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
  name: '${storage.name}/default/images'
  properties: {
    publicAccess: 'Blob'
  }
}

resource containerapp 'Microsoft.App/containerApps@2022-03-01' = {
  name: '${name}publisher'
  location: location
  tags: union(tags, { 'azd-service-name': '${name}publisher' })
  properties: {
    managedEnvironmentId: env.id
    configuration: {
      activeRevisionsMode: 'Single'
      dapr: {
        appId: 'image-upload'
        appPort: 3000
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
        external: ingress
        targetPort: 3000
      }
    }
    template: {
      containers: [
        {
          image: image
          name: '${name}publisher'
          env: [
            {
              name: 'AZURE_STORAGE_CONNECTION_STRING'
              value: 'DefaultEndpointsProtocol=https;AccountName=${storage.name};AccountKey=${storage.listKeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}'
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
              name: 'HUB_HOST'
              value: 'https://${name}subscriber.${env.properties.defaultDomain}'
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
