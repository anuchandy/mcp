// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Mcp.Core.Areas.Server.Commands.Runtime;
using Azure.Mcp.Tools.Quota.Services.Util;

namespace Azure.Mcp.Tools.Quota.Services;

public interface IQuotaService
{
    Task<Dictionary<string, List<UsageInfo>>> GetAzureQuotaAsync(
        McpUserContext userContext,
        List<string> resourceTypes,
        string subscriptionId,
        string location);

    Task<List<string>> GetAvailableRegionsForResourceTypesAsync(
        McpUserContext userContext,
        string[] resourceTypes,
        string subscriptionId,
        string? cognitiveServiceModelName = null,
        string? cognitiveServiceModelVersion = null,
        string? cognitiveServiceDeploymentSkuName = null);
}
