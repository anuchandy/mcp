// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Mcp.Core.Areas.Server.Commands.Runtime;

/// <summary>
/// Factory interface for creating Azure MCP request contexts.
/// </summary>
/// <remarks>
/// Known implementations:
/// <para><see cref="DefaultRequestContextFactory"/>Creates AzMcpRequestContext implementation without user identity for non-OBO scenarios</para>
/// <para><see cref="OboParentRequestContextFactory"/>Creates AzMcpRequestContext by extracting user identity from HTTP context</para>
/// <para><see cref="OboChildRequestContextFactory"/>Creates AzMcpRequestContext by extracting user identity from tool call arguments</para>
/// </remarks>
public interface IAzMcpRequestContextFactory
{
    AzMcpRequestContext<TParams> Create<TParams>(RequestContext<TParams> raw) where TParams : class;
}
