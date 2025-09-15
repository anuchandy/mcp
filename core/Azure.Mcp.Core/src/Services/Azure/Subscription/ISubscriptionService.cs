// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Mcp.Core.Areas.Server.Commands.Runtime;
using Azure.Mcp.Core.Options;
using Azure.ResourceManager.Resources;

namespace Azure.Mcp.Core.Services.Azure.Subscription;

public interface ISubscriptionService
{
    Task<List<SubscriptionData>> GetSubscriptions(McpUserContext userContext, string? tenant = null, RetryPolicyOptions? retryPolicy = null);
    Task<SubscriptionResource> GetSubscription(McpUserContext userContext, string subscription, string? tenant = null, RetryPolicyOptions? retryPolicy = null);
    bool IsSubscriptionId(string subscription, string? tenant = null);
    Task<string> GetSubscriptionIdByName(McpUserContext userContext, string subscriptionName, string? tenant = null, RetryPolicyOptions? retryPolicy = null);
    Task<string> GetSubscriptionNameById(McpUserContext userContext, string subscriptionId, string? tenant = null, RetryPolicyOptions? retryPolicy = null);
}
