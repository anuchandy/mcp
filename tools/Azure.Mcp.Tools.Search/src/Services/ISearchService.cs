// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Mcp.Core.Areas.Server.Commands.Runtime;
using Azure.Mcp.Core.Options;
using Azure.Mcp.Tools.Search.Models;
using static Azure.Mcp.Tools.Search.Commands.Index.IndexDescribeCommand;

namespace Azure.Mcp.Tools.Search.Services;

public interface ISearchService
{
    Task<List<string>> ListServices(
        McpUserContext userContext,
        string subscription,
        string? tenantId = null,
        RetryPolicyOptions? retryPolicy = null);

    Task<List<IndexInfo>> ListIndexes(
        McpUserContext userContext,
        string serviceName,
        RetryPolicyOptions? retryPolicy = null);

    Task<SearchIndexProxy?> DescribeIndex(
        McpUserContext userContext,
        string serviceName,
        string indexName,
        RetryPolicyOptions? retryPolicy = null);

    Task<List<JsonElement>> QueryIndex(
        McpUserContext userContext,
        string serviceName,
        string indexName,
        string searchText,
        RetryPolicyOptions? retryPolicy = null);
}
