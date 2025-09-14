// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Security.Claims;
using ModelContextProtocol.Protocol;

namespace Azure.Mcp.Core.Areas.Server.Commands.Runtime;

/// <summary>
/// Extended request context that carries per-request Azure MCP metadata (session, tenant, user, correlation)
/// in addition to the underlying protocol <see cref="RequestContext{TParams}"/>.
/// Parent processes populate all fields; child processes receive a reduced envelope (no principal).
/// </summary>
/// <typeparam name="TParams">Parameter type for the underlying MCP request.</typeparam>
public sealed class AzMcpRequestContext<TParams>
{
    private readonly RequestContext<TParams> _inner; 

    public AzMcpRequestContext(
        RequestContext<TParams> inner,
        string sessionId,
        string correlationId,
        string? tenantId,
        string? userObjectId,
        AzRuntimeMode role,
        DateTimeOffset timestampUtc,
        string? serializedClaimsPrincipal = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        SessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));
        CorrelationId = correlationId ?? throw new ArgumentNullException(nameof(correlationId));
        TenantId = tenantId;
        UserObjectId = userObjectId;
        Role = role;
        TimestampUtc = timestampUtc;
        SerializedClaimsPrincipal = serializedClaimsPrincipal;
    }

    /// <summary>Underlying protocol server.</summary>
    public IMcpServer Server => _inner.Server;

    /// <summary>Parameters of the underlying protocol request.</summary>
    public TParams? Params => _inner.Params;

    /// <summary>Internal accessor preserved for code that still needs the raw context within this assembly.</summary>
    internal RequestContext<TParams> ProtocolContext => _inner;

    /// <summary>Opaque per-user session identifier (parent generated).</summary>
    public string SessionId { get; }

    /// <summary>Per-request correlation id.</summary>
    public string CorrelationId { get; }

    // ParentCorrelationId removed: child processes no longer carry spawn lineage.

    /// <summary>Azure AD tenant id (optional for single-tenant scenarios).</summary>
    public string? TenantId { get; }

    /// <summary>Azure AD user object id (optional).</summary>
    public string? UserObjectId { get; }

    /// <summary>Runtime role of this process for the request.</summary>
    public AzRuntimeMode Role { get; }

    /// <summary>UTC timestamp when context was created.</summary>
    public DateTimeOffset TimestampUtc { get; }

    /// <summary>
    /// Serialized ClaimsPrincipal from the authenticated user (OBO Parent mode only).
    /// This contains the full claims context for optimal token caching in child processes.
    /// Stored as Base64-encoded string for JSON compatibility.
    /// </summary>
    public string? SerializedClaimsPrincipal { get; }

    /// <summary>
    /// Deserializes the ClaimsPrincipal from SerializedClaimsPrincipal if available.
    /// </summary>
    /// <returns>The deserialized ClaimsPrincipal, or null if not available.</returns>
    public ClaimsPrincipal? GetClaimsPrincipal()
    {
        if (string.IsNullOrEmpty(SerializedClaimsPrincipal))
            return null;

        try
        {
            var bytes = Convert.FromBase64String(SerializedClaimsPrincipal);
            using var stream = new MemoryStream(bytes);
            using var reader = new BinaryReader(stream);
            return new ClaimsPrincipal(reader);
        }
        catch
        {
            // If deserialization fails, return null to gracefully degrade
            return null;
        }
    }
}

/// <summary>Runtime mode of the current azmcp process.</summary>
public enum AzRuntimeMode
{
    Default = 0,
    OboParent = 1,
    OboChild = 2,
}
