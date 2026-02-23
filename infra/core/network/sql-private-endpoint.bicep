targetScope = 'resourceGroup'

@description('Private endpoint resource name.')
param name string

@description('Primary location for resources.')
param location string = resourceGroup().location

@description('Tags to apply to resources.')
param tags object = {}

@description('Resource ID of the subnet where the private endpoint will be created.')
param subnetResourceId string

@description('Resource ID of private DNS zone for SQL private link.')
param privateDnsZoneId string

@description('Resource ID of the private link service (SQL server).')
param privateLinkServiceId string

resource privateEndpoint 'Microsoft.Network/privateEndpoints@2023-11-01' = {
  name: name
  location: location
  tags: tags
  properties: {
    subnet: {
      id: subnetResourceId
    }
    privateLinkServiceConnections: [
      {
        name: 'sqlServerConnection'
        properties: {
          privateLinkServiceId: privateLinkServiceId
          groupIds: [
            'sqlServer'
          ]
        }
      }
    ]
  }
}

resource privateEndpointDnsZoneGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2023-11-01' = {
  parent: privateEndpoint
  name: 'default'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'sqlPrivateDnsZone'
        properties: {
          privateDnsZoneId: privateDnsZoneId
        }
      }
    ]
  }
}

output id string = privateEndpoint.id
