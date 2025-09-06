// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;

namespace Azure.Mcp.Core.Areas.Server.Commands.Runtime;

/// <summary>
/// IAzMcpRequestContextFactory implementation for default Azure MCP usage (non-OBO).
/// </summary>
internal sealed class DefaultRequestContextFactory : IAzMcpRequestContextFactory
{
    private readonly ILogger<DefaultRequestContextFactory> _logger;

    public DefaultRequestContextFactory(ILogger<DefaultRequestContextFactory> logger)
    {
        _logger = logger;
    }

    public AzMcpRequestContext<TParams> Create<TParams>(RequestContext<TParams> raw) where TParams : class
    {
        if (raw == null)
            throw new ArgumentNullException(nameof(raw));

            // In Default mode, no user identity enrichment
            // DefaultAzureCredential will handle authentication based on environment
            return new AzMcpRequestContext<TParams>(
                raw,
                tenantId: null,
                userObjectId: null,
                role: AzRuntimeMode.Default, // Default mode doesn't have parent/child distinction
                serializedClaimsPrincipal: null);
    }
}