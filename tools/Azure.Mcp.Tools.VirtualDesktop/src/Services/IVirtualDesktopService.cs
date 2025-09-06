// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Mcp.Core.Areas.Server.Commands.Runtime;
using Azure.Mcp.Tools.VirtualDesktop.Models;

namespace Azure.Mcp.Tools.VirtualDesktop.Services;

using Azure.Mcp.Core.Options;

public interface IVirtualDesktopService
{
    Task<IReadOnlyList<HostPool>> ListHostpoolsAsync(McpUserContext userContext, string subscription, string? tenant = null, RetryPolicyOptions? retryPolicy = null);
    Task<IReadOnlyList<HostPool>> ListHostpoolsByResourceGroupAsync(McpUserContext userContext, string subscription, string resourceGroup, string? tenant = null, RetryPolicyOptions? retryPolicy = null);
    Task<IReadOnlyList<SessionHost>> ListSessionHostsAsync(McpUserContext userContext, string subscription, string hostPoolName, string? tenant = null, RetryPolicyOptions? retryPolicy = null);
    Task<IReadOnlyList<SessionHost>> ListSessionHostsByResourceIdAsync(McpUserContext userContext, string subscription, string hostPoolResourceId, string? tenant = null, RetryPolicyOptions? retryPolicy = null);
    Task<IReadOnlyList<SessionHost>> ListSessionHostsByResourceGroupAsync(McpUserContext userContext, string subscription, string resourceGroup, string hostPoolName, string? tenant = null, RetryPolicyOptions? retryPolicy = null);
    Task<IReadOnlyList<UserSession>> ListUserSessionsAsync(McpUserContext userContext, string subscription, string hostPoolName, string sessionHostName, string? tenant = null, RetryPolicyOptions? retryPolicy = null);
    Task<IReadOnlyList<UserSession>> ListUserSessionsByResourceIdAsync(McpUserContext userContext, string subscription, string hostPoolResourceId, string sessionHostName, string? tenant = null, RetryPolicyOptions? retryPolicy = null);
    Task<IReadOnlyList<UserSession>> ListUserSessionsByResourceGroupAsync(McpUserContext userContext, string subscription, string resourceGroup, string hostPoolName, string sessionHostName, string? tenant = null, RetryPolicyOptions? retryPolicy = null);
}
