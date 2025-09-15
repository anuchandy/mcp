// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Mcp.Core.Areas.Server.Commands.Runtime;
using Azure.Mcp.Core.Options;
using Azure.Mcp.Tools.Kusto.Commands;

namespace Azure.Mcp.Tools.Kusto.Services;

public interface IKustoService
{
    Task<List<string>> ListClusters(
        McpUserContext userContext,
        string subscription,
        string? tenant = null,
        RetryPolicyOptions? retryPolicy = null);

    Task<KustoClusterResourceProxy?> GetCluster(
        McpUserContext userContext,
        string subscription,
        string clusterName,
        string? tenant = null,
        RetryPolicyOptions? retryPolicy = null);

    Task<List<string>> ListDatabases(
        McpUserContext userContext,
        string clusterUri,
        string? tenant = null,
        AuthMethod? authMethod = AuthMethod.Credential,
        RetryPolicyOptions? retryPolicy = null);

    Task<List<string>> ListDatabases(
        McpUserContext userContext,
        string subscription,
        string clusterName,
        string? tenant = null,
        AuthMethod? authMethod = AuthMethod.Credential,
        RetryPolicyOptions? retryPolicy = null);

    Task<List<JsonElement>> QueryItems(
        McpUserContext userContext,
        string clusterUri,
        string databaseName,
        string query,
        string? tenant = null,
        AuthMethod? authMethod = AuthMethod.Credential,
        RetryPolicyOptions? retryPolicy = null);

    Task<List<JsonElement>> QueryItems(
        McpUserContext userContext,
        string subscriptionId,
        string clusterName,
        string databaseName,
        string query,
        string? tenant = null,
        AuthMethod? authMethod = AuthMethod.Credential,
        RetryPolicyOptions? retryPolicy = null);

    Task<List<string>> ListTables(
        McpUserContext userContext,
        string clusterUri,
        string databaseName,
        string? tenant = null,
        AuthMethod? authMethod = AuthMethod.Credential,
        RetryPolicyOptions? retryPolicy = null);

    Task<List<string>> ListTables(
        McpUserContext userContext,
        string subscriptionId,
        string clusterName,
        string databaseName,
        string? tenant = null,
        AuthMethod? authMethod = AuthMethod.Credential,
        RetryPolicyOptions? retryPolicy = null);

    Task<string> GetTableSchema(
        McpUserContext userContext,
        string clusterUri,
        string databaseName,
        string tableName,
        string? tenant = null,
        AuthMethod? authMethod = AuthMethod.Credential,
        RetryPolicyOptions? retryPolicy = null);

    Task<string> GetTableSchema(
        McpUserContext userContext,
        string subscriptionId,
        string clusterName,
        string databaseName,
        string tableName,
        string? tenant = null,
        AuthMethod? authMethod = AuthMethod.Credential,
        RetryPolicyOptions? retryPolicy = null);
}
