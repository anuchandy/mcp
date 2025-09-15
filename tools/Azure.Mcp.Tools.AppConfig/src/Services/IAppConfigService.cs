// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Mcp.Core.Areas.Server.Commands.Runtime;
using Azure.Mcp.Core.Options;
using Azure.Mcp.Tools.AppConfig.Models;

namespace Azure.Mcp.Tools.AppConfig.Services;

public interface IAppConfigService
{
    Task<List<AppConfigurationAccount>> GetAppConfigAccounts(
        McpUserContext userContext,
        string subscription,
        string? tenant = null,
        RetryPolicyOptions? retryPolicy = null);
    Task<List<KeyValueSetting>> ListKeyValues(
        McpUserContext userContext,
        string accountName,
        string subscription,
        string? key = null, string? label = null,
        string? tenant = null,
        RetryPolicyOptions? retryPolicy = null);
    Task<KeyValueSetting> GetKeyValue(
        McpUserContext userContext,
        string accountName,
        string key,
        string subscription,
        string? tenant = null,
        RetryPolicyOptions? retryPolicy = null,
        string? label = null,
        string? contentType = null);
    Task LockKeyValue(
        McpUserContext userContext,
        string accountName,
        string key,
        string subscription,
        string? tenant = null,
        RetryPolicyOptions? retryPolicy = null,
        string? label = null);
    Task UnlockKeyValue(
        McpUserContext userContext,
        string accountName,
        string key,
        string subscription,
        string? tenant = null,
        RetryPolicyOptions? retryPolicy = null,
        string? label = null);
    Task SetKeyValue(
        McpUserContext userContext,
        string accountName,
        string key,
        string value,
        string subscription,
        string? tenant = null,
        RetryPolicyOptions? retryPolicy = null,
        string? label = null,
        string? contentType = null,
        string[]? tags = null);
    Task DeleteKeyValue(
        McpUserContext userContext,
        string accountName,
        string key,
        string subscription,
        string? tenant = null,
        RetryPolicyOptions? retryPolicy = null,
        string? label = null);
}
