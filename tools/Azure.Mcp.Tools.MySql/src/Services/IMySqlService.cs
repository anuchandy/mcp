// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Mcp.Core.Areas.Server.Commands.Runtime;

namespace Azure.Mcp.Tools.MySql.Services;

public interface IMySqlService
{
    Task<List<string>> ListDatabasesAsync(McpUserContext userContext, string subscriptionId, string resourceGroup, string user, string server);
    Task<List<string>> ExecuteQueryAsync(McpUserContext userContext, string subscriptionId, string resourceGroup, string user, string server, string database, string query);

    Task<List<string>> GetTablesAsync(McpUserContext userContext, string subscriptionId, string resourceGroup, string user, string server, string database);
    Task<List<string>> GetTableSchemaAsync(McpUserContext userContext, string subscriptionId, string resourceGroup, string user, string server, string database, string table);

    Task<List<string>> ListServersAsync(McpUserContext userContext, string subscriptionId, string resourceGroup, string user);
    Task<string> GetServerConfigAsync(McpUserContext userContext, string subscriptionId, string resourceGroup, string user, string server);
    Task<string> GetServerParameterAsync(McpUserContext userContext, string subscriptionId, string resourceGroup, string user, string server, string param);
    Task<string> SetServerParameterAsync(McpUserContext userContext, string subscription, string resourceGroup, string user, string server, string param, string value);
}
