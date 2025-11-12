targetScope = 'resourceGroup'

@description('The principal ID of the Container App managed identity')
param acaPrincipalId string

@description('The name of the Key Vault')
param keyVaultName string = 'anuchankv321'

// Reference the existing Key Vault in this resource group
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: keyVaultName
}

// Grant Container App managed identity access to read secrets from Key Vault
// Certificates in Key Vault are accessed as secrets when retrieving the PFX bytes
resource keyVaultSecretsUserRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, acaPrincipalId, 'Key Vault Secrets User')
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6') // Key Vault Secrets User
    principalId: acaPrincipalId
    principalType: 'ServicePrincipal'
  }
}

output roleAssignmentId string = keyVaultSecretsUserRole.id
