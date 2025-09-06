// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Security.Claims;
using ModelContextProtocol.Protocol;

namespace Azure.Mcp.Core.Areas.Server.Commands.Runtime;

/// <summary>
/// Extended request context that carries per-request Azure MCP metadata (tenant, user, claims principal)
/// in addition to the underlying protocol <see cref="RequestContext{TParams}"/>.
/// </summary>
/// <typeparam name="TParams">Parameter type for the underlying MCP request.</typeparam>
public sealed class AzMcpRequestContext<TParams>
{
    private readonly RequestContext<TParams> _inner; 

    public AzMcpRequestContext(
        RequestContext<TParams> inner,
        string? tenantId,
        string? userObjectId,
        AzRuntimeMode role,
        string? serializedClaimsPrincipal = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        TenantId = tenantId;
        UserObjectId = userObjectId;
        Role = role;
        SerializedClaimsPrincipal = serializedClaimsPrincipal;
    }

    /// <summary>Underlying protocol server.</summary>
    public IMcpServer Server => _inner.Server;

    /// <summary>Parameters of the underlying protocol request.</summary>
    public TParams? Params => _inner.Params;

    /// <summary>Runtime role of this process for the request.</summary>
    public AzRuntimeMode Role { get; }

    /// <summary>Azure AD tenant id (optional for single-tenant scenarios).</summary>
    public string? TenantId { get; }

    /// <summary>Azure AD user object id (optional).</summary>
    public string? UserObjectId { get; }

    /// <summary>
    /// Serialized ClaimsPrincipal from the authenticated user, stored as Base64-encoded string.
    /// </summary>
    public string? SerializedClaimsPrincipal { get; }
}
