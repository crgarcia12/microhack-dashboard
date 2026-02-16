@description('Name of the SQL Server')
param name string

@description('Location for the SQL Server')
param location string = resourceGroup().location

@description('Tags for all resources')
param tags object = {}

@description('Name of the SQL Database')
param databaseName string = 'hackboxdb'

@description('Object ID of the Azure AD user to set as SQL admin')
param aadAdminObjectId string

@description('Login name (UPN) of the Azure AD admin')
param aadAdminLogin string

resource sqlServer 'Microsoft.Sql/servers@2024-11-01-preview' = {
  name: name
  location: location
  tags: tags
  properties: {
    version: '12.0'
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
    administrators: {
      administratorType: 'ActiveDirectory'
      principalType: 'User'
      login: aadAdminLogin
      sid: aadAdminObjectId
      tenantId: tenant().tenantId
      azureADOnlyAuthentication: true
    }
  }
}

// Allow Azure services to access the SQL Server
resource firewallRule 'Microsoft.Sql/servers/firewallRules@2024-11-01-preview' = {
  parent: sqlServer
  name: 'AllowAllAzureIps'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource database 'Microsoft.Sql/servers/databases@2024-11-01-preview' = {
  parent: sqlServer
  name: databaseName
  location: location
  tags: tags
  sku: {
    name: 'Basic'
    tier: 'Basic'
    capacity: 5
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    maxSizeBytes: 2147483648 // 2 GB
  }
}

output id string = sqlServer.id
output name string = sqlServer.name
output fullyQualifiedDomainName string = sqlServer.properties.fullyQualifiedDomainName
output databaseName string = database.name
