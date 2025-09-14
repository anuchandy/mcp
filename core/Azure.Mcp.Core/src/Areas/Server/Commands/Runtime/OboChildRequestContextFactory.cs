// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Mcp.Core.Areas.Server.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;

namespace Azure.Mcp.Core.Areas.Server.Commands.Runtime;

/// <summary>
/// OBO Child implementation that extracts user identity from tool call arguments
/// provided by the parent process via the OBO channel.
/// </summary>
internal sealed class OboChildRequestContextFactory : IAzMcpRequestContextFactory
{
    private readonly ILogger<OboChildRequestContextFactory> _logger;

    public OboChildRequestContextFactory(ILogger<OboChildRequestContextFactory> logger)
    {
        _logger = logger;
        _logger.LogInformation("Runtime operating in OBO CHILD mode; user identity fields will be extracted from tool arguments.");
    }

    public AzMcpRequestContext<TParams> Create<TParams>(RequestContext<TParams> raw) where TParams : class
    {
        if (raw == null)
            throw new ArgumentNullException(nameof(raw));

        // For now, always create new session per request (could reuse across connection in future)
        var sessionId = $"session-{Guid.NewGuid():n}";
        var correlationId = Guid.NewGuid().ToString("n");

        string? tenantId = null;
        string? userObjectId = null;
        string? serializedClaimsPrincipal = null;

        try
        {
            // Only applies to CallTool requests; ListTools has no arguments.
            if (typeof(TParams) == typeof(CallToolRequestParams) && raw.Params is CallToolRequestParams callParams && callParams.Arguments != null)
            {
                foreach (var kvp in callParams.Arguments)
                {
                    if (tenantId == null && string.Equals(kvp.Key, "tenantId", StringComparison.OrdinalIgnoreCase) && kvp.Value.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        tenantId = kvp.Value.GetString();
                    }
                    else if (userObjectId == null && string.Equals(kvp.Key, "userObjectId", StringComparison.OrdinalIgnoreCase) && kvp.Value.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        userObjectId = kvp.Value.GetString();
                    }
                    else if (serializedClaimsPrincipal == null && string.Equals(kvp.Key, "serializedClaimsPrincipal", StringComparison.OrdinalIgnoreCase) && kvp.Value.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        serializedClaimsPrincipal = kvp.Value.GetString();
                    }
                    if (tenantId != null && userObjectId != null && serializedClaimsPrincipal != null)
                    {
                        break; // all found
                    }
                }

                if (tenantId != null || userObjectId != null)
                {
                    _logger.LogDebug("Extracted identity from tool arguments (tenant: {TenantPresent}, user: {UserPresent}, serializedPrincipal: {PrincipalPresent}).", tenantId != null, userObjectId != null, serializedClaimsPrincipal != null);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "OBO child identity extraction from tool arguments failed; continuing without identity.");
        }

        return new AzMcpRequestContext<TParams>(
            raw,
            sessionId: sessionId,
            correlationId: correlationId,
            tenantId: tenantId,
            userObjectId: userObjectId,
            role: AzRuntimeMode.OboChild,
            timestampUtc: DateTimeOffset.UtcNow,
            serializedClaimsPrincipal: serializedClaimsPrincipal);
    }
}