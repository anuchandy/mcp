// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Mcp.Core.Services.Azure.Authentication;

/// <summary>
/// Enumeration of Azure services that require different authentication scopes.
/// </summary>
/// <remarks>
/// This enumeration maps Azure services to their corresponding OAuth 2.0 scopes
/// for use in On-Behalf-Of (OBO) token acquisition scenarios.
/// </remarks>
public enum AzureService
{
    /// <summary>
    /// Azure Resource Manager (management plane operations).
    /// Scope: https://management.azure.com/.default
    /// </summary>
    /// <remarks>
    /// Used for Azure Resource Manager operations such as managing subscriptions,
    /// resource groups, and resource providers.
    /// </remarks>
    ResourceManager,

    /// <summary>
    /// Azure Storage (data plane operations).
    /// Scope: https://storage.azure.com/.default
    /// </summary>
    /// <remarks>
    /// Used for Azure Storage data plane operations such as blob, table, queue,
    /// and file share access.
    /// </remarks>
    StorageData,

    /// <summary>
    /// Azure Key Vault (data plane operations).
    /// Scope: https://vault.azure.com/.default
    /// </summary>
    /// <remarks>
    /// Used for Azure Key Vault data plane operations such as accessing secrets,
    /// keys, and certificates.
    /// </remarks>
    KeyVaultData,

    /// <summary>
    /// Azure Cosmos DB (data plane operations).
    /// Scope: https://cosmos.azure.com/.default
    /// </summary>
    /// <remarks>
    /// Used for Azure Cosmos DB data plane operations such as database and
    /// container management.
    /// </remarks>
    CosmosDb,

    /// <summary>
    /// Azure Database for PostgreSQL.
    /// Scope: https://ossrdbms-aad.database.windows.net/.default
    /// </summary>
    /// <remarks>
    /// Used for Azure Database for PostgreSQL authentication.
    /// </remarks>
    PostgreSQL,

    /// <summary>
    /// Azure Database for MySQL.
    /// Scope: https://ossrdbms-aad.database.windows.net/.default
    /// </summary>
    /// <remarks>
    /// Used for Azure Database for MySQL authentication.
    /// </remarks>
    MySQL,

    /// <summary>
    /// Azure Service Bus.
    /// Scope: https://servicebus.azure.net/.default
    /// </summary>
    /// <remarks>
    /// Used for Azure Service Bus operations such as sending and receiving messages.
    /// </remarks>
    ServiceBus
}
