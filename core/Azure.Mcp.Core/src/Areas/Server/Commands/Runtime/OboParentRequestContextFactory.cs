// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Azure.Mcp.Core.Areas.Server.Commands.Runtime;

/// <summary>
/// OBO Parent implementation that extracts user identity from HTTP context.
/// </summary>
internal sealed class OboParentRequestContextFactory : IAzMcpRequestContextFactory
{
    private readonly ILogger<OboParentRequestContextFactory> _logger;

    public OboParentRequestContextFactory(ILogger<OboParentRequestContextFactory> logger)
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
            var services = raw.Server?.Services;
            var accessor = services?.GetService(typeof(IHttpContextAccessor)) as IHttpContextAccessor;
            var principal = accessor?.HttpContext?.User;
            if (principal?.Identity?.IsAuthenticated == true)
            {
                tenantId = ExtractTenantId(principal);
                userObjectId = ExtractUserObjectId(principal);

                try
                {
                    // Serialize the full ClaimsPrincipal
                    using var stream = new MemoryStream();
                    using var writer = new BinaryWriter(stream);
                    principal.WriteTo(writer);
                    var bytes = stream.ToArray();
                    serializedClaimsPrincipal = Convert.ToBase64String(bytes);
                }
                catch (Exception serEx)
                {
                    _logger.LogDebug(serEx, "Failed to serialize ClaimsPrincipal."); // throw?
                }

                if (tenantId != null || userObjectId != null)
                {
                    _logger.LogDebug("Extracted identity claims (tenant: {TenantPresent}, user: {UserPresent}).", tenantId != null, userObjectId != null);
                }
                else
                {
                    _logger.LogDebug("ClaimsPrincipal present but required claims (tenantId, userObjectId) missing.");
                }
            }
            else if (principal != null)
            {
                _logger.LogDebug("ClaimsPrincipal present but not authenticated."); // throw?
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to extract identity claims."); // throw?
        }

        return new AzMcpRequestContext<TParams>(
            raw,
            tenantId: tenantId,
            userObjectId: userObjectId,
            role: AzRuntimeMode.OboParent,
            serializedClaimsPrincipal: serializedClaimsPrincipal);
    }

    private static string? ExtractTenantId(ClaimsPrincipal principal)
    {
        return principal.FindFirst("tid")?.Value
               ?? principal.FindFirst("http://schemas.microsoft.com/identity/claims/tenantid")?.Value;
    }

    private static string? ExtractUserObjectId(ClaimsPrincipal principal)
    {
        return principal.FindFirst("oid")?.Value
               ?? principal.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value
               ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
               ?? principal.FindFirst("sub")?.Value;
    }
}