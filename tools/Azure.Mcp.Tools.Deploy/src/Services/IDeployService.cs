// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Mcp.Core.Areas.Server.Commands.Runtime;

namespace Azure.Mcp.Tools.Deploy.Services;

public interface IDeployService
{
    Task<string> GetAzdResourceLogsAsync(
        McpUserContext userContext,
        string workspaceFolder,
        string azdEnvName,
        string subscriptionId,
        int? limit = null);
}
