// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Mcp.Core.Areas.Server.Commands.Runtime;
using Azure.Mcp.Core.Models.ResourceGroup;
using Azure.Mcp.Core.Options;
using Azure.ResourceManager.Resources;

namespace Azure.Mcp.Core.Services.Azure.ResourceGroup;

public interface IResourceGroupService
{
    Task<List<ResourceGroupInfo>> GetResourceGroups(McpUserContext userContext, string subscriptionId, string? tenant = null, RetryPolicyOptions? retryPolicy = null);
    Task<ResourceGroupInfo?> GetResourceGroup(McpUserContext userContext, string subscriptionId, string resourceGroupName, string? tenant = null, RetryPolicyOptions? retryPolicy = null);
    Task<ResourceGroupResource?> GetResourceGroupResource(McpUserContext userContext, string subscriptionId, string resourceGroupName, string? tenant = null, RetryPolicyOptions? retryPolicy = null);
}
