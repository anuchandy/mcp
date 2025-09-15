// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Mcp.Core.Areas.Server.Commands.Runtime;
using Azure.Mcp.Core.Options;
using Azure.Mcp.Tools.Workbooks.Models;

namespace Azure.Mcp.Tools.Workbooks.Services;

public interface IWorkbooksService
{
    Task<List<WorkbookInfo>> ListWorkbooks(McpUserContext userContext, string subscription, string resourceGroupName, WorkbookFilters? filters = null, RetryPolicyOptions? retryPolicy = null, string? tenant = null);
    Task<WorkbookInfo?> CreateWorkbook(McpUserContext userContext, string subscription, string resourceGroupName, string displayName, string serializedData, string sourceId, RetryPolicyOptions? retryPolicy = null, string? tenant = null);
    Task<WorkbookInfo?> GetWorkbook(McpUserContext userContext, string workbookId, RetryPolicyOptions? retryPolicy = null, string? tenant = null);
    Task<WorkbookInfo?> UpdateWorkbook(McpUserContext userContext, string workbookId, string? displayName = null, string? serializedContent = null, RetryPolicyOptions? retryPolicy = null, string? tenant = null);
    Task<bool> DeleteWorkbook(McpUserContext userContext, string workbookId, RetryPolicyOptions? retryPolicy = null, string? tenant = null);
}
