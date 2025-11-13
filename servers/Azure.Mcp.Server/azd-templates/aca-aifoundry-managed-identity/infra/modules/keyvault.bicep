@description('Location for the Key Vault')
param location string = resourceGroup().location

@description('Name prefix for resources')
param name string

@description('Certificate name')
param certificateName string = 'mcp-server-internal-https-cert'

var keyVaultName = 'kv-${uniqueString(resourceGroup().id, name)}'

resource keyVault 'Microsoft.KeyVault/vaults@2024-11-01' = {
  name: keyVaultName
  location: location
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
  }
}

// Managed identity for deployment script
resource managedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-cert-creator-${name}'
  location: location
}

// Grant managed identity permission to create certificates in Key Vault
resource roleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, managedIdentity.id, 'a4417e6f-fecd-4de8-b567-7b0420556985')
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'a4417e6f-fecd-4de8-b567-7b0420556985') // Key Vault Certificates Officer
    principalId: managedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// Deployment script to create certificate using Azure CLI
resource createCertScript 'Microsoft.Resources/deploymentScripts@2023-08-01' = {
  name: 'create-cert-${certificateName}'
  location: location
  kind: 'AzureCLI'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${managedIdentity.id}': {}
    }
  }
  properties: {
    azCliVersion: '2.52.0'
    retentionInterval: 'PT1H'
    timeout: 'PT5M'
    cleanupPreference: 'OnSuccess'
    environmentVariables: [
      {
        name: 'VAULT_NAME'
        value: keyVault.name
      }
      {
        name: 'CERT_NAME'
        value: certificateName
      }
    ]
    scriptContent: '''
      echo "Creating certificate ${CERT_NAME} in vault ${VAULT_NAME}..."
      
      az keyvault certificate create \
        --vault-name "${VAULT_NAME}" \
        --name "${CERT_NAME}" \
        --policy '{
          "issuerParameters": {"name": "Self"},
          "keyProperties": {
            "exportable": true,
            "keyType": "RSA",
            "keySize": 2048,
            "reuseKey": false
          },
          "secretProperties": {"contentType": "application/x-pkcs12"},
          "x509CertificateProperties": {
            "subject": "CN=localhost",
            "validityInMonths": 12,
            "ekus": ["1.3.6.1.5.5.7.3.1"],
            "keyUsage": ["digitalSignature", "keyEncipherment"],
            "subjectAlternativeNames": {
              "dnsNames": ["localhost", "*.dev.localhost", "*.dev.internal", "host.docker.internal", "host.containers.internal"]
            }
          }
        }'
      
      echo "Certificate created successfully"
    '''
  }
  dependsOn: [
    roleAssignment
  ]
}

output keyVaultName string = keyVault.name
output keyVaultId string = keyVault.id
output certificateName string = certificateName
