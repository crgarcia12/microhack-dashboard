targetScope = 'resourceGroup'

@description('Virtual network name.')
param virtualNetworkName string

@description('Primary location for resources.')
param location string = resourceGroup().location

@description('Tags to apply to resources.')
param tags object = {}

@description('Container Apps infrastructure subnet name.')
param containerAppsInfrastructureSubnetName string = 'snet-ca-infra'

@description('Container Apps infrastructure subnet CIDR.')
param containerAppsInfrastructureSubnetAddressPrefix string = '10.0.0.0/23'

@description('Network security group name for Container Apps infrastructure subnet.')
param containerAppsInfrastructureNsgName string = 'nsg-ca-infra'

@description('SQL private endpoint subnet name.')
param sqlPrivateEndpointSubnetName string = 'snet-sql-pe'

@description('SQL private endpoint subnet CIDR.')
param sqlPrivateEndpointSubnetAddressPrefix string = '10.0.2.0/24'

@description('SQL private DNS zone name.')
param sqlPrivateDnsZoneName string = 'privatelink.database.windows.net'

resource containerAppsInfrastructureNsg 'Microsoft.Network/networkSecurityGroups@2023-11-01' = {
  name: containerAppsInfrastructureNsgName
  location: location
  tags: tags
  properties: {
    securityRules: [
      {
        name: 'AllowAllInternetInbound'
        properties: {
          priority: 100
          direction: 'Inbound'
          access: 'Allow'
          protocol: '*'
          sourcePortRange: '*'
          destinationPortRange: '*'
          sourceAddressPrefix: 'Internet'
          destinationAddressPrefix: '*'
        }
      }
    ]
  }
}

resource vnet 'Microsoft.Network/virtualNetworks@2023-11-01' = {
  name: virtualNetworkName
  location: location
  tags: tags
  properties: {
    addressSpace: {
      addressPrefixes: [
        '10.0.0.0/16'
      ]
    }
  }
}

resource containerAppsInfrastructureSubnet 'Microsoft.Network/virtualNetworks/subnets@2023-11-01' = {
  parent: vnet
  name: containerAppsInfrastructureSubnetName
  properties: {
    addressPrefix: containerAppsInfrastructureSubnetAddressPrefix
    networkSecurityGroup: {
      id: containerAppsInfrastructureNsg.id
    }
    delegations: [
      {
        name: 'containerAppsDelegation'
        properties: {
          serviceName: 'Microsoft.App/environments'
        }
      }
    ]
  }
}

resource sqlPrivateEndpointSubnet 'Microsoft.Network/virtualNetworks/subnets@2023-11-01' = {
  parent: vnet
  name: sqlPrivateEndpointSubnetName
  properties: {
    addressPrefix: sqlPrivateEndpointSubnetAddressPrefix
    privateEndpointNetworkPolicies: 'Disabled'
  }
}

resource sqlPrivateDnsZone 'Microsoft.Network/privateDnsZones@2020-06-01' = {
  name: sqlPrivateDnsZoneName
  location: 'global'
  tags: tags
}

resource sqlPrivateDnsZoneLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2020-06-01' = {
  parent: sqlPrivateDnsZone
  name: '${virtualNetworkName}-link'
  location: 'global'
  properties: {
    registrationEnabled: false
    virtualNetwork: {
      id: vnet.id
    }
  }
}

output virtualNetworkId string = vnet.id
output containerAppsInfrastructureSubnetId string = containerAppsInfrastructureSubnet.id
output sqlPrivateEndpointSubnetId string = sqlPrivateEndpointSubnet.id
output sqlPrivateDnsZoneId string = sqlPrivateDnsZone.id
