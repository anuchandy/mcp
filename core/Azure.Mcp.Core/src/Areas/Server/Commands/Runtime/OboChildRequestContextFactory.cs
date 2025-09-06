// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;

namespace Azure.Mcp.Core.Areas.Server.Commands.Runtime;

/// <summary>
/// OBO Child implementation that extracts user identity from tool call arguments provided by the parent process.
/// </summary>
internal sealed class OboChildRequestContextFactory : IAzMcpRequestContextFactory
{
    private readonly ILogger<OboChildRequestContextFactory> _logger;

    public OboChildRequestContextFactory(ILogger<OboChildRequestContextFactory> logger)
    {
        _logger = logger;
    }

    public AzMcpRequestContext<TParams> Create<TParams>(RequestContext<TParams> raw) where TParams : class
    {
        if (raw == null)
            throw new ArgumentNullException(nameof(raw));

        string? tenantId = null;
        string? userObjectId = null;
        string? serializedClaimsPrincipal = null;

        try
        {
            // Only applies to CallTool requests; ListTools has no arguments.
            if (typeof(TParams) == typeof(CallToolRequestParams)
                && raw.Params is CallToolRequestParams callParams
                && callParams.Arguments != null)
            {
                foreach (var kvp in callParams.Arguments)
                {
                    if (tenantId == null && HasKey(kvp, "tenantId"))
                    {
                        tenantId = kvp.Value.GetString();
                    }
                    else if (userObjectId == null && HasKey(kvp, "userObjectId"))
                    {
                        userObjectId = kvp.Value.GetString();
                    }
                    else if (serializedClaimsPrincipal == null && HasKey(kvp, "serializedClaimsPrincipal"))
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
                    _logger.LogDebug(
                        "Extracted identity from tool arguments: tenant={Tenant}, user={User}, principal={PrincipalPresent}",
                        tenantId ?? "null", 
                        userObjectId ?? "null", 
                        serializedClaimsPrincipal != null);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "OBO child identity extraction from tool arguments failed."); // throw?
        }

        return new AzMcpRequestContext<TParams>(
            raw,
            tenantId: tenantId,
            userObjectId: userObjectId,
            role: AzRuntimeMode.OboChild,
            serializedClaimsPrincipal: serializedClaimsPrincipal);
    }

    private static bool HasKey(KeyValuePair<string, System.Text.Json.JsonElement> kvp, string expectedKey)
    {
        if (kvp.Value.ValueKind != System.Text.Json.JsonValueKind.String)
        {
            return false;
        }
        return string.Equals(kvp.Key, expectedKey, StringComparison.OrdinalIgnoreCase);
    }
}