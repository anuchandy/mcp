// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Mcp.Core.Areas.Server.Commands.Runtime;
using Azure.Mcp.Core.Services.Azure;
using Azure.Mcp.Core.Services.Azure.ResourceGroup;
using Azure.Mcp.Core.Services.Azure.Subscription;
using Azure.Mcp.Core.Services.Azure.Tenant;
using Azure.Mcp.Core.Services.Caching;
using Azure.Mcp.Tools.FunctionApp.Models;
using Azure.ResourceManager.AppService;

namespace Azure.Mcp.Tools.FunctionApp.Services;

public sealed class FunctionAppService(
    ISubscriptionService subscriptionService,
    ITenantService tenantService,
    ICacheService2 cacheService,
    IResourceGroupService resourceGroupService) : BaseAzureService(tenantService), IFunctionAppService
{
    private readonly ISubscriptionService _subscriptionService = subscriptionService ?? throw new ArgumentNullException(nameof(subscriptionService));
    private readonly ICacheService2 _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
    private readonly IResourceGroupService _resourceGroupService = resourceGroupService ?? throw new ArgumentNullException(nameof(resourceGroupService));

    private const string CacheGroup = "functionapp";
    private static readonly TimeSpan s_cacheDuration = TimeSpan.FromHours(1);

    public async Task<List<FunctionAppInfo>?> ListFunctionApps(
        McpUserContext userContext,
        string subscription,
        string? tenant = null,
        RetryPolicyOptions? retryPolicy = null)
    {
        ValidateRequiredParameters(subscription);

        var cacheKey = string.IsNullOrEmpty(tenant)
            ? subscription
            : $"{subscription}_{tenant}";

        var userGroup = userContext.GroupKey();
        var serviceGroup = "functionapp";

        var cachedResults = _cacheService.GetOrCreate<List<FunctionAppInfo>>(userGroup, serviceGroup, cacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = s_cacheDuration;
            
            return GetFunctionAppsFromAzureAsync(userContext, subscription, tenant, retryPolicy).GetAwaiter().GetResult();
        });

        return await Task.FromResult(cachedResults);
    }

    private async Task<List<FunctionAppInfo>> GetFunctionAppsFromAzureAsync(
        McpUserContext userContext,
        string subscription,
        string? tenant,
        RetryPolicyOptions? retryPolicy)
    {
        var subscriptionResource = await _subscriptionService.GetSubscription(userContext, subscription, tenant, retryPolicy);
        var functionApps = new List<FunctionAppInfo>();

        try
        {
            await foreach (var site in subscriptionResource.GetWebSitesAsync())
            {
                if (site?.Data != null && IsFunctionApp(site.Data))
                {
                    functionApps.Add(ConvertToFunctionAppModel(site));
                }
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Error retrieving Function Apps: {ex.Message}", ex);
        }

        return functionApps;
    }

    public async Task<FunctionAppInfo?> GetFunctionApp(
        McpUserContext userContext,
        string subscription,
        string functionAppName,
        string resourceGroup,
        string? tenant = null,
        RetryPolicyOptions? retryPolicy = null)
    {
        ValidateRequiredParameters(subscription, functionAppName, resourceGroup);

        var cacheKey = string.IsNullOrEmpty(tenant)
            ? $"{subscription}_{resourceGroup}_{functionAppName}"
            : $"{subscription}_{tenant}_{resourceGroup}_{functionAppName}";

        var userGroup = userContext.GroupKey();
        var serviceGroup = "functionapp";

        var cachedResults = _cacheService.GetOrCreate<FunctionAppInfo?>(userGroup, serviceGroup, cacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = s_cacheDuration;
            
            return GetFunctionAppFromAzureAsync(userContext, subscription, functionAppName, resourceGroup, tenant, retryPolicy).GetAwaiter().GetResult();
        });

        return await Task.FromResult(cachedResults);
    }

    private async Task<FunctionAppInfo?> GetFunctionAppFromAzureAsync(
        McpUserContext userContext,
        string subscription,
        string functionAppName,
        string resourceGroup,
        string? tenant,
        RetryPolicyOptions? retryPolicy)
    {
        try
        {
            var rg = await _resourceGroupService.GetResourceGroupResource(userContext, subscription, resourceGroup, tenant, retryPolicy);
            if (rg is null)
            {
                return null;
            }
            var site = await rg.GetWebSites().GetAsync(functionAppName);

            if (site?.Value?.Data is null || !IsFunctionApp(site.Value.Data))
            {
                return null;
            }

            return ConvertToFunctionAppModel(site.Value);
        }
        catch (Exception ex)
        {
            throw new Exception($"Error retrieving Function App '{functionAppName}' in resource group '{resourceGroup}': {ex.Message}", ex);
        }
    }

    private static bool IsFunctionApp(WebSiteData siteData)
    {
        return siteData.Kind?.Contains("functionapp", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static FunctionAppInfo ConvertToFunctionAppModel(WebSiteResource siteResource)
    {
        var data = siteResource.Data;

        return new FunctionAppInfo(
            data.Name,
            siteResource.Id.ResourceGroupName,
            data.Location.ToString(),
            data.AppServicePlanId.Name,
            data.State,
            data.DefaultHostName,
            data.Tags?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
        );
    }
}
