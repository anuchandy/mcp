// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Mcp.Core.Areas.Server.Commands.Runtime;
using Azure.Mcp.Core.Options;
using Azure.Mcp.Core.Services.Azure;
using Azure.Mcp.Core.Services.Azure.Subscription;
using Azure.Mcp.Core.Services.Caching;
using Azure.Mcp.Tools.Search.Commands;
using Azure.Mcp.Tools.Search.Models;
using Azure.ResourceManager.Search;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using static Azure.Mcp.Tools.Search.Commands.Index.IndexDescribeCommand;

namespace Azure.Mcp.Tools.Search.Services;

public sealed class SearchService(ISubscriptionService subscriptionService, ICacheService2 cacheService) : BaseAzureService, ISearchService
{
    private readonly ISubscriptionService _subscriptionService = subscriptionService ?? throw new ArgumentNullException(nameof(subscriptionService));
    private readonly ICacheService2 _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
    private const string CacheGroup = "search";
    private const string SearchServicesCacheKey = "services";
    private const string SearchClientsCacheKeyPrefix = "clients_";
    private static readonly TimeSpan s_cacheDurationServices = TimeSpan.FromHours(1);
    private static readonly TimeSpan s_cacheDurationClients = TimeSpan.FromMinutes(15);

    public async Task<List<string>> ListServices(
        McpUserContext userContext,
        string subscription,
        string? tenantId = null,
        RetryPolicyOptions? retryPolicy = null)
    {
        ValidateRequiredParameters(subscription);

        var cacheKey = string.IsNullOrEmpty(tenantId)
            ? $"{SearchServicesCacheKey}_{subscription}"
            : $"{SearchServicesCacheKey}_{subscription}_{tenantId}";

        var userGroup = userContext.GroupKey();
        var serviceGroup = "search";

        var cachedServices = _cacheService.GetOrCreate<List<string>>(userGroup, serviceGroup, cacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = s_cacheDurationServices;
            
            return GetSearchServicesFromAzureAsync(userContext, subscription, tenantId, retryPolicy).GetAwaiter().GetResult();
        });

        return await Task.FromResult(cachedServices);
    }

    private async Task<List<string>> GetSearchServicesFromAzureAsync(
        McpUserContext userContext,
        string subscription,
        string? tenantId,
        RetryPolicyOptions? retryPolicy)
    {
        var subscriptionResource = await _subscriptionService.GetSubscription(userContext, subscription, tenantId, retryPolicy);
        var services = new List<string>();
        try
        {
            await foreach (var service in subscriptionResource.GetSearchServicesAsync())
            {
                if (service?.Data?.Name != null)
                {
                    services.Add(service.Data.Name);
                }
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Error retrieving Search services: {ex.Message}", ex);
        }

        return services;
    }

    public async Task<List<IndexInfo>> ListIndexes(
        McpUserContext userContext,
        string serviceName,
        RetryPolicyOptions? retryPolicy = null)
    {
        ValidateRequiredParameters(serviceName);

        var indexes = new List<IndexInfo>();

        try
        {
            var searchClient = await GetSearchIndexClient(userContext,serviceName, retryPolicy);
            await foreach (var index in searchClient.GetIndexesAsync())
            {
                indexes.Add(new IndexInfo(index.Name, index.Description));
            }
            return indexes;
        }
        catch (Exception ex)
        {
            throw new Exception($"Error retrieving Search indexes: {ex.Message}", ex);
        }
    }

    public async Task<SearchIndexProxy?> DescribeIndex(
        McpUserContext userContext,
        string serviceName,
        string indexName,
        RetryPolicyOptions? retryPolicy = null)
    {
        ValidateRequiredParameters(serviceName, indexName);

        try
        {
            var searchClient = await GetSearchIndexClient(userContext,serviceName, retryPolicy);
            var index = await searchClient.GetIndexAsync(indexName);

            return new(index.Value);
        }
        catch (Exception ex)
        {
            throw new Exception($"Error retrieving Search index details: {ex.Message}", ex);
        }
    }

    public async Task<List<JsonElement>> QueryIndex(
        McpUserContext userContext,
        string serviceName,
        string indexName,
        string searchText,
        RetryPolicyOptions? retryPolicy = null)
    {
        ValidateRequiredParameters(serviceName, indexName, searchText);

        try
        {
            var searchClient = await GetSearchIndexClient(userContext,serviceName, retryPolicy);
            var indexDefinition = await searchClient.GetIndexAsync(indexName);
            var client = searchClient.GetSearchClient(indexName);

            var options = new SearchOptions
            {
                IncludeTotalCount = true,
                Size = 20
            };

            var vectorFields = FindVectorFields(indexDefinition.Value);
            var vectorizableFields = FindVectorizableFields(indexDefinition.Value, vectorFields);
            ConfigureSearchOptions(searchText, options, indexDefinition.Value, vectorFields);

            var searchResponse = await client.SearchAsync(searchText, SearchJsonContext.Default.JsonElement, options);

            return await ProcessSearchResults(searchResponse);
        }
        catch (Exception ex)
        {
            throw new Exception($"Error querying Search index: {ex.Message}", ex);
        }
    }

    private static List<string> FindVectorFields(SearchIndex indexDefinition)
    {
        return [.. indexDefinition.Fields
                    .Where(f => f.VectorSearchDimensions.HasValue)
                    .Select(f => f.Name)];
    }

    private static List<string> FindVectorizableFields(SearchIndex indexDefinition, List<string> vectorFields)
    {
        var vectorizableFields = new List<string>();

        if (indexDefinition.VectorSearch?.Profiles == null || indexDefinition.VectorSearch.Algorithms == null)
        {
            return vectorizableFields;
        }

        foreach (var field in indexDefinition.Fields)
        {
            if (vectorFields.Contains(field.Name) && !string.IsNullOrEmpty(field.VectorSearchProfileName))
            {
                var profile = indexDefinition.VectorSearch.Profiles
                    .FirstOrDefault(p => p.Name == field.VectorSearchProfileName);

                if (profile != null)
                {
                    if (!string.IsNullOrEmpty(profile.VectorizerName))
                    {
                        vectorizableFields.Add(field.Name);
                    }
                }
            }
        }

        return vectorizableFields;
    }

    private async Task<SearchIndexClient> GetSearchIndexClient(McpUserContext userContext, string serviceName, RetryPolicyOptions? retryPolicy)
    {
        var key = SearchClientsCacheKeyPrefix + serviceName;
        var userGroup = userContext.GroupKey();
        var serviceGroup = "search";

        var searchClient = _cacheService.GetOrCreate<SearchIndexClient>(userGroup, serviceGroup, key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = s_cacheDurationClients;
            
            return CreateSearchIndexClientAsync(userContext, serviceName, retryPolicy).GetAwaiter().GetResult();
        });
        
        return await Task.FromResult(searchClient);
    }

    private async Task<SearchIndexClient> CreateSearchIndexClientAsync(
        McpUserContext userContext,
        string serviceName,
        RetryPolicyOptions? retryPolicy)
    {
        var credential = await GetCredential(userContext);

        var clientOptions = AddDefaultPolicies(new SearchClientOptions());
        ConfigureRetryPolicy(clientOptions, retryPolicy);

        var endpoint = new Uri($"https://{serviceName}.search.windows.net");
        return new SearchIndexClient(endpoint, credential, clientOptions);
    }

    private static void ConfigureSearchOptions(string q, SearchOptions options, SearchIndex indexDefinition, List<string> vectorFields)
    {
        List<string> selectedFields = [.. indexDefinition.Fields
                                                         .Where(f => f.IsHidden == false && !vectorFields.Contains(f.Name))
                                                         .Select(f => f.Name)];
        foreach (var field in selectedFields)
        {
            options.Select.Add(field);
        }

        options.VectorSearch = new VectorSearchOptions();
        foreach (var vf in vectorFields)
        {
            options.VectorSearch.Queries.Add(new VectorizableTextQuery(q) { Fields = { vf }, KNearestNeighborsCount = 50 });
        }
    }

    private static async Task<List<JsonElement>> ProcessSearchResults(Response<SearchResults<JsonElement>> searchResponse)
    {
        var results = new List<JsonElement>();
        await foreach (var result in searchResponse.Value.GetResultsAsync())
        {
            results.Add(result.Document);
        }
        return results;
    }

    private static void ConfigureRetryPolicy(SearchClientOptions options, RetryPolicyOptions? retryPolicy)
    {
        if (retryPolicy != null)
        {
            options.Retry.MaxRetries = retryPolicy.MaxRetries;
            options.Retry.Mode = retryPolicy.Mode;
            options.Retry.Delay = TimeSpan.FromSeconds(retryPolicy.DelaySeconds);
            options.Retry.MaxDelay = TimeSpan.FromSeconds(retryPolicy.MaxDelaySeconds);
            options.Retry.NetworkTimeout = TimeSpan.FromSeconds(retryPolicy.NetworkTimeoutSeconds);
        }
    }
}
