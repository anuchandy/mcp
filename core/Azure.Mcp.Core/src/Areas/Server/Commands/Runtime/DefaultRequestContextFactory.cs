// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Mcp.Core.Areas.Server.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;

namespace Azure.Mcp.Core.Areas.Server.Commands.Runtime;

/// <summary>
/// Default implementation for standard Azure MCP usage (non-OBO).
/// Uses DefaultAzureCredential flow and generates session/correlation IDs.
/// </summary>
internal sealed class DefaultRequestContextFactory : IAzMcpRequestContextFactory
{
    private readonly ILogger<DefaultRequestContextFactory> _logger;

    public DefaultRequestContextFactory(ILogger<DefaultRequestContextFactory> logger)
    {
        _logger = logger;
        _logger.LogInformation("Runtime operating in standard mode; using DefaultAzureCredential flow.");
    }

    public AzMcpRequestContext<TParams> Create<TParams>(RequestContext<TParams> raw) where TParams : class
    {
        if (raw == null)
            throw new ArgumentNullException(nameof(raw));

        // For now, always create new session per request (could reuse across connection in future)
        var sessionId = $"session-{Guid.NewGuid():n}";
        var correlationId = Guid.NewGuid().ToString("n");

        // In standard mode, no user identity enrichment
        // DefaultAzureCredential will handle authentication based on environment
        return new AzMcpRequestContext<TParams>(
            raw,
            sessionId: sessionId,
            correlationId: correlationId,
            tenantId: null,
            userObjectId: null,
            role: AzRuntimeMode.Default, // Standard mode doesn't have parent/child distinction
            timestampUtc: DateTimeOffset.UtcNow,
            serializedClaimsPrincipal: null);
    }
}