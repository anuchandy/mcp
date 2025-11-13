@description('Location for the Key Vault')
param location string = resourceGroup().location

@description('Name prefix for resources')
param name string

@description('Certificate name')
param certificateName string = 'mcp-server-internal-https-cert'

var keyVaultName = 'kv-${uniqueString(resourceGroup().id, name)}'

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
    softDeleteRetentionInDays: 7
  }
}

// Create self-signed certificate 
resource certificate 'Microsoft.KeyVault/vaults/certificates@2023-07-01' = {
  parent: keyVault
  name: certificateName
  properties: {
    certificatePolicy: {
      issuerParameters: {
        name: 'Self'
      }
      secretProperties: {
        contentType: 'application/x-pkcs12'
      }
      keyProperties: {
        exportable: true
        keyType: 'RSA'
        keySize: 2048
        reuseKey: false
      }
      x509CertificateProperties: {
        subject: 'CN=localhost'
        validityInMonths: 12
        ekus: [
          '1.3.6.1.5.5.7.3.1'
        ]
        keyUsage: [
          'digitalSignature'
          'keyEncipherment'
        ]
        subjectAlternativeNames: {
          dnsNames: [
            'localhost'
            '*.dev.localhost'
            '*.dev.internal'
            'host.docker.internal'
            'host.containers.internal'
          ]
        }
      }
    }
  }
}

output keyVaultName string = keyVault.name
output keyVaultId string = keyVault.id
output certificateName string = certificate.name
