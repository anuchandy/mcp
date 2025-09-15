// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Mcp.Core.Areas.Server.Commands.Runtime;
using Azure.Mcp.Core.Options;

namespace Azure.Mcp.Tools.Cosmos.Services;

public interface ICosmosService : IDisposable
{
    Task<List<string>> GetCosmosAccounts(
        McpUserContext userContext,
        string subscription,
        string? tenant = null,
        RetryPolicyOptions? retryPolicy = null);

    Task<List<string>> ListDatabases(
        McpUserContext userContext,
        string accountName,
        string subscription,
        AuthMethod authMethod = AuthMethod.Credential,
        string? tenant = null,
        RetryPolicyOptions? retryPolicy = null);

    Task<List<string>> ListContainers(
        McpUserContext userContext,
        string accountName,
        string databaseName,
        string subscription,
        AuthMethod authMethod = AuthMethod.Credential,
        string? tenant = null,
        RetryPolicyOptions? retryPolicy = null);

    Task<List<JsonElement>> QueryItems(
        McpUserContext userContext,
        string accountName,
        string databaseName,
        string containerName,
        string? query,
        string subscription,
        AuthMethod authMethod = AuthMethod.Credential,
        string? tenant = null,
        RetryPolicyOptions? retryPolicy = null);
}
