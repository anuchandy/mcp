// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Mcp.Core.Areas.Server.Commands.Runtime;

namespace Azure.Mcp.Tools.Acr.Services;

public interface IAcrService
{
    Task<List<Models.AcrRegistryInfo>> ListRegistries(
        McpUserContext userContext,
        string subscription,
        string? resourceGroup = null,
        string? tenant = null,
        RetryPolicyOptions? retryPolicy = null);

    Task<Dictionary<string, List<string>>> ListRegistryRepositories(
        McpUserContext userContext,
        string subscription,
        string? resourceGroup = null,
        string? registry = null,
        string? tenant = null,
        RetryPolicyOptions? retryPolicy = null);
}
