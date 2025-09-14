// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Security.Claims;
using Azure.Mcp.Core.Areas.Server.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;

namespace Azure.Mcp.Core.Areas.Server.Commands.Runtime;

/// <summary>
/// OBO Parent implementation that extracts user identity from HTTP context
/// and brokers authentication for child processes.
/// </summary>
internal sealed class OboParentRequestContextFactory : IAzMcpRequestContextFactory
{
    private readonly ILogger<OboParentRequestContextFactory> _logger;

    public OboParentRequestContextFactory(ILogger<OboParentRequestContextFactory> logger)
    {
        _logger = logger;
        _logger.LogInformation("Runtime operating in OBO PARENT mode; will extract user identity from HTTP context for child processes.");
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

        try
        {
            var services = raw.Server?.Services;
            var accessor = services?.GetService(typeof(IHttpContextAccessor)) as IHttpContextAccessor;
            var principal = accessor?.HttpContext?.User;
            if (principal?.Identity?.IsAuthenticated == true)
            {
                tenantId = principal.FindFirst("tid")?.Value
                           ?? principal.FindFirst("http://schemas.microsoft.com/identity/claims/tenantid")?.Value;

                userObjectId = principal.FindFirst("oid")?.Value
                               ?? principal.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value
                               ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                               ?? principal.FindFirst("sub")?.Value;

                if (tenantId != null || userObjectId != null)
                {
                    _logger.LogDebug("Extracted identity claims (tenant: {TenantPresent}, user: {UserPresent}).", tenantId != null, userObjectId != null);
                }
                else
                {
                    _logger.LogDebug("Authenticated principal present but required claims missing.");
                }
            }
            else if (principal != null)
            {
                _logger.LogDebug("Principal present but not authenticated; skipping identity enrichment.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to extract identity claims; continuing without identity.");
        }

        return new AzMcpRequestContext<TParams>(
            raw,
            sessionId: sessionId,
            correlationId: correlationId,
            tenantId: tenantId,
            userObjectId: userObjectId,
            role: AzRuntimeMode.OboParent,
            timestampUtc: DateTimeOffset.UtcNow);
    }
}