// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using ModelContextProtocol.Protocol;

namespace Azure.Mcp.Core.Areas.Server.Commands.Runtime;

/// <summary>
/// Factory interface for creating Azure MCP request contexts.
/// </summary>
public interface IAzMcpRequestContextFactory
{
    AzMcpRequestContext<TParams> Create<TParams>(RequestContext<TParams> raw) where TParams : class;
}
