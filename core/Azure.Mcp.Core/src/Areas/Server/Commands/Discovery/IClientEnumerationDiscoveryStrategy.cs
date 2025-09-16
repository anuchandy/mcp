// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using ModelContextProtocol.Client;

namespace Azure.Mcp.Core.Areas.Server.Commands.Discovery;

/// <summary>
/// Internal interface to expose enumeration of cached MCP clients for scenarios like recursive sign-out.
/// </summary>
internal interface IClientEnumerationDiscoveryStrategy : IMcpDiscoveryStrategy
{
    /// <summary>
    /// Returns a snapshot enumeration of currently cached MCP clients.
    /// </summary>
    IEnumerable<IMcpClient> GetCachedClients();
}
