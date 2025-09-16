// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Mcp.Core.Areas.Server.Commands.Runtime;
using Azure.Mcp.Core.Options;
using Azure.Mcp.Core.Services.Azure;
using Azure.Mcp.Core.Services.Azure.Subscription;
using Azure.Mcp.Core.Services.Azure.Tenant;
using Azure.Mcp.Core.Services.Caching;
using Azure.ResourceManager.CosmosDB;
using Microsoft.Azure.Cosmos;

namespace Azure.Mcp.Tools.Cosmos.Services;

public class CosmosService(ISubscriptionService subscriptionService, ITenantService tenantService, ICacheService2 cacheService)
    : BaseAzureService(tenantService), ICosmosService, IDisposable
{
    private readonly ISubscriptionService _subscriptionService = subscriptionService ?? throw new ArgumentNullException(nameof(subscriptionService));
    private readonly ICacheService2 _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
    private const string CosmosBaseUri = "https://{0}.documents.azure.com:443/";
    private const string CosmosClientsCacheKeyPrefix = "clients_";
    private const string CosmosDatabasesCacheKeyPrefix = "databases_";
    private const string CosmosContainersCacheKeyPrefix = "containers_";
    private static readonly TimeSpan s_cacheDurationResources = TimeSpan.FromMinutes(15);
    private bool _disposed;

    private async Task<CosmosDBAccountResource> GetCosmosAccountAsync(
        McpUserContext userContext,
        string subscription,
        string accountName,
        string? tenant = null,
        RetryPolicyOptions? retryPolicy = null)
    {
        ValidateRequiredParameters(subscription, accountName);

        var subscriptionResource = await _subscriptionService.GetSubscription(userContext, subscription, tenant, retryPolicy);

        await foreach (var account in subscriptionResource.GetCosmosDBAccountsAsync())
        {
            if (account.Data.Name == accountName)
            {
                return account;
            }
        }
        throw new Exception($"Cosmos DB account '{accountName}' not found in subscription '{subscription}'");
    }

    private async Task<CosmosClient> CreateCosmosClientWithAuth(
        McpUserContext userContext,
        string accountName,
        string subscription,
        AuthMethod authMethod,
        string? tenant = null,
        RetryPolicyOptions? retryPolicy = null)
    {
        // Enable bulk execution and distributed tracing telemetry features once they are supported by the Microsoft.Azure.Cosmos.Aot package.
        // var clientOptions = new CosmosClientOptions { AllowBulkExecution = true };
        // clientOptions.CosmosClientTelemetryOptions.DisableDistributedTracing = false;
        var clientOptions = new CosmosClientOptions();
        clientOptions.CustomHandlers.Add(new UserPolicyRequestHandler(UserAgent));

        if (retryPolicy != null)
        {
            clientOptions.MaxRetryAttemptsOnRateLimitedRequests = retryPolicy.MaxRetries;
            clientOptions.MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(retryPolicy.MaxDelaySeconds);
        }

        CosmosClient cosmosClient;
        switch (authMethod)
        {
            case AuthMethod.Key:
                var cosmosAccount = await GetCosmosAccountAsync(userContext, subscription, accountName, tenant);
                var keys = await cosmosAccount.GetKeysAsync();
                cosmosClient = new CosmosClient(
                    string.Format(CosmosBaseUri, accountName),
                    keys.Value.PrimaryMasterKey,
                    clientOptions);
                break;

            case AuthMethod.Credential:
            default:
                cosmosClient = new CosmosClient(
                    string.Format(CosmosBaseUri, accountName),
                    await GetCredential(userContext, tenant),
                    clientOptions);
                break;
        }

        // Validate the client by performing a lightweight operation
        await ValidateCosmosClientAsync(cosmosClient);

        return cosmosClient;
    }

    private async Task ValidateCosmosClientAsync(CosmosClient client)
    {
        try
        {
            // Perform a lightweight operation to validate the client
            await client.ReadAccountAsync();
        }
        catch (CosmosException ex)
        {
            throw new Exception($"Failed to validate CosmosClient: {ex.StatusCode} - {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            throw new Exception($"Unexpected error while validating CosmosClient: {ex.Message}", ex);
        }
    }

    private Task<CosmosClient> GetCosmosClientAsync(
        McpUserContext userContext,
        string accountName,
        string subscription,
        AuthMethod authMethod = AuthMethod.Credential,
        string? tenant = null,
        RetryPolicyOptions? retryPolicy = null)
    {
        ValidateRequiredParameters(accountName, subscription);

        var key = CosmosClientsCacheKeyPrefix + accountName;
        var userGroup = userContext.GroupKey();
        var serviceGroup = "cosmos";

        var result = _cacheService.GetOrCreate<CosmosClient>(userGroup, serviceGroup, key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = s_cacheDurationResources;
            
            return CreateCosmosClientWithFallback(userContext, accountName, subscription, authMethod, tenant, retryPolicy).GetAwaiter().GetResult();
        });
        
        return Task.FromResult(result);
    }

    private async Task<CosmosClient> CreateCosmosClientWithFallback(
        McpUserContext userContext,
        string accountName,
        string subscription,
        AuthMethod authMethod,
        string? tenant,
        RetryPolicyOptions? retryPolicy)
    {
        try
        {
            // First attempt with requested auth method
            return await CreateCosmosClientWithAuth(
                userContext,
                accountName,
                subscription,
                authMethod,
                tenant,
                retryPolicy);
        }
        catch (Exception ex) when (
            authMethod == AuthMethod.Credential &&
            (ex.Message.Contains("401") || ex.Message.Contains("403")))
        {
            // If credential auth fails with 401/403, try key auth
            return await CreateCosmosClientWithAuth(
                userContext,
                accountName,
                subscription,
                AuthMethod.Key,
                tenant,
                retryPolicy);
        }
    }

    public async Task<List<string>> GetCosmosAccounts(McpUserContext userContext, string subscription, string? tenant = null, RetryPolicyOptions? retryPolicy = null)
    {
        ValidateRequiredParameters(subscription);

        var subscriptionResource = await _subscriptionService.GetSubscription(userContext, subscription, tenant, retryPolicy);
        var accounts = new List<string>();
        try
        {
            await foreach (var account in subscriptionResource.GetCosmosDBAccountsAsync())
            {
                if (account?.Data?.Name != null)
                {
                    accounts.Add(account.Data.Name);
                }
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Error retrieving Cosmos DB accounts: {ex.Message}", ex);
        }

        return accounts;
    }

    public Task<List<string>> ListDatabases(
        McpUserContext userContext,
        string accountName,
        string subscription,
        AuthMethod authMethod = AuthMethod.Credential,
        string? tenant = null,
        RetryPolicyOptions? retryPolicy = null)
    {
        ValidateRequiredParameters(accountName, subscription);

        var cacheKey = CosmosDatabasesCacheKeyPrefix + accountName;
        var userGroup = userContext.GroupKey();
        var serviceGroup = "cosmos";

        var result = _cacheService.GetOrCreate<List<string>>(userGroup, serviceGroup, cacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = s_cacheDurationResources;
            
            return GetDatabasesFromCosmosAsync(userContext, accountName, subscription, authMethod, tenant, retryPolicy).GetAwaiter().GetResult();
        });
        
        return Task.FromResult(result);
    }

    private async Task<List<string>> GetDatabasesFromCosmosAsync(
        McpUserContext userContext,
        string accountName,
        string subscription,
        AuthMethod authMethod,
        string? tenant,
        RetryPolicyOptions? retryPolicy)
    {
        var client = await GetCosmosClientAsync(userContext, accountName, subscription, authMethod, tenant, retryPolicy);
        var databases = new List<string>();

        try
        {
            var iterator = client.GetDatabaseQueryStreamIterator();
            while (iterator.HasMoreResults)
            {
                ResponseMessage dbResponse = await iterator.ReadNextAsync();
                if (!dbResponse.IsSuccessStatusCode)
                {
                    throw new Exception(dbResponse.ErrorMessage);
                }
                using JsonDocument dbsQueryResultDoc = JsonDocument.Parse(dbResponse.Content);
                if (dbsQueryResultDoc.RootElement.TryGetProperty("Databases", out JsonElement documentsElement))
                {
                    foreach (JsonElement databaseElement in documentsElement.EnumerateArray())
                    {
                        string? databaseId = databaseElement.GetProperty("id").GetString();
                        if (!string.IsNullOrEmpty(databaseId))
                        {
                            databases.Add(databaseId);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Error listing databases in the account '{accountName}': {ex.Message}", ex);
        }

        return databases;
    }

    public Task<List<string>> ListContainers(
        McpUserContext userContext,
        string accountName,
        string databaseName,
        string subscription,
        AuthMethod authMethod = AuthMethod.Credential,
        string? tenant = null,
        RetryPolicyOptions? retryPolicy = null)
    {
        ValidateRequiredParameters(accountName, databaseName, subscription);

        var cacheKey = CosmosContainersCacheKeyPrefix + accountName + "_" + databaseName;
        var userGroup = userContext.GroupKey();
        var serviceGroup = "cosmos";

        var result = _cacheService.GetOrCreate<List<string>>(userGroup, serviceGroup, cacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = s_cacheDurationResources;
            
            return GetContainersFromCosmosAsync(userContext, accountName, databaseName, subscription, authMethod, tenant, retryPolicy).GetAwaiter().GetResult();
        });
        
        return Task.FromResult(result);
    }

    private async Task<List<string>> GetContainersFromCosmosAsync(
        McpUserContext userContext,
        string accountName,
        string databaseName,
        string subscription,
        AuthMethod authMethod,
        string? tenant,
        RetryPolicyOptions? retryPolicy)
    {
        var client = await GetCosmosClientAsync(userContext, accountName, subscription, authMethod, tenant, retryPolicy);
        var containers = new List<string>();

        try
        {
            var database = client.GetDatabase(databaseName);
            var iterator = database.GetContainerQueryStreamIterator();
            while (iterator.HasMoreResults)
            {
                ResponseMessage containerRResponse = await iterator.ReadNextAsync();
                if (!containerRResponse.IsSuccessStatusCode)
                {
                    throw new Exception(containerRResponse.ErrorMessage);
                }
                using JsonDocument containersQueryResultDoc = JsonDocument.Parse(containerRResponse.Content);
                if (containersQueryResultDoc.RootElement.TryGetProperty("DocumentCollections", out JsonElement containersElement))
                {
                    foreach (JsonElement containerElement in containersElement.EnumerateArray())
                    {
                        string? containerId = containerElement.GetProperty("id").GetString();
                        if (!string.IsNullOrEmpty(containerId))
                        {
                            containers.Add(containerId);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Error listing containers in database '{databaseName}' of account '{accountName}': {ex.Message}", ex);
        }

        return containers;
    }

    public async Task<List<JsonElement>> QueryItems(
        McpUserContext userContext,
        string accountName,
        string databaseName,
        string containerName,
        string? query,
        string subscription,
        AuthMethod authMethod = AuthMethod.Credential,
        string? tenant = null,
        RetryPolicyOptions? retryPolicy = null)
    {
        ValidateRequiredParameters(accountName, databaseName, containerName, subscription);

        var client = await GetCosmosClientAsync(userContext, accountName, subscription, authMethod, tenant, retryPolicy);

        try
        {
            var container = client.GetContainer(databaseName, containerName);
            var baseQuery = string.IsNullOrEmpty(query) ? "SELECT * FROM c" : query;
            var queryDef = new QueryDefinition(baseQuery);

            var items = new List<JsonElement>();
            var queryIterator = container.GetItemQueryStreamIterator(
                queryDef,
                requestOptions: new QueryRequestOptions { MaxItemCount = -1 }
            );

            while (queryIterator.HasMoreResults)
            {
                var response = await queryIterator.ReadNextAsync();
                using var document = JsonDocument.Parse(response.Content);
                items.Add(document.RootElement.Clone());
            }

            return items;
        }
        catch (CosmosException ex)
        {
            throw new Exception($"Cosmos DB error occurred while querying items: {ex.StatusCode} - {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            throw new Exception($"Error querying items: {ex.Message}", ex);
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // Get all CosmosClient objects for the "cosmos" service group and dispose them
                var cosmosClients = _cacheService.GetValuesByGroup1<CosmosClient>("cosmos");
                
                foreach (var kvp in cosmosClients)
                {
                    // Check if this is a CosmosClient cache entry by looking at the local key
                    if (kvp.Key.LocalKey.StartsWith(CosmosClientsCacheKeyPrefix))
                    {
                        kvp.Value?.Dispose();
                    }
                }
                _disposed = true;
            }
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    internal class UserPolicyRequestHandler : RequestHandler
    {
        private readonly string userAgent;

        internal UserPolicyRequestHandler(string userAgent) => this.userAgent = userAgent;

        public override Task<ResponseMessage> SendAsync(RequestMessage request, CancellationToken cancellationToken)
        {
            request.Headers.Set(UserAgentPolicy.UserAgentHeader, userAgent);
            return base.SendAsync(request, cancellationToken);
        }
    }
}
