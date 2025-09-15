// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Security.Claims;

namespace Azure.Mcp.Core.Services.Azure.Authentication;

/// <summary>
/// Represents the user authentication context for MCP requests, containing 
/// user identity information required for OBO token acquisition.
/// </summary>
public sealed class McpUserContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="McpUserContext"/> class.
    /// </summary>
    /// <param name="claimsPrincipal">The ClaimsPrincipal representing the authenticated user.</param>
    /// <param name="serializedClaimsPrincipal">The Base64-encoded serialized ClaimsPrincipal for inter-process communication.</param>
    /// <param name="tenantId">The Azure AD tenant identifier.</param>
    /// <param name="userObjectId">The Azure AD user object identifier.</param>
    /// <param name="sessionId">The unique session identifier for this user context.</param>
    public McpUserContext(
        ClaimsPrincipal? claimsPrincipal = null,
        string? serializedClaimsPrincipal = null,
        string? tenantId = null,
        string? userObjectId = null,
        string? sessionId = null)
    {
        ClaimsPrincipal = claimsPrincipal;
        SerializedClaimsPrincipal = serializedClaimsPrincipal;
        TenantId = tenantId;
        UserObjectId = userObjectId;
        SessionId = sessionId;
    }

    /// <summary>
    /// Gets the ClaimsPrincipal representing the authenticated user.
    /// This is used in OBO Parent scenarios for direct token acquisition.
    /// </summary>
    public ClaimsPrincipal? ClaimsPrincipal { get; }

    /// <summary>
    /// Gets the Base64-encoded serialized ClaimsPrincipal for inter-process communication.
    /// This is used in OBO Child scenarios for named pipe token requests.
    /// </summary>
    public string? SerializedClaimsPrincipal { get; }

    /// <summary>
    /// Gets the Azure AD tenant identifier for the authenticated user.
    /// </summary>
    public string? TenantId { get; }

    /// <summary>
    /// Gets the Azure AD user object identifier for the authenticated user.
    /// </summary>
    public string? UserObjectId { get; }

    /// <summary>
    /// Gets the unique session identifier for this user context.
    /// This can be used for caching and correlation purposes.
    /// </summary>
    public string? SessionId { get; }

    /// <summary>
    /// Gets a value indicating whether this context represents an authenticated user.
    /// </summary>
    public bool IsAuthenticated => ClaimsPrincipal?.Identity?.IsAuthenticated == true || 
                                  !string.IsNullOrEmpty(SerializedClaimsPrincipal);

    /// <summary>
    /// Determines if this user context supports OBO (On-Behalf-Of) authentication flow.
    /// This requires the user to be authenticated and have both tenant ID and user object ID.
    /// </summary>
    public bool SupportsOboFlow => IsAuthenticated && !string.IsNullOrEmpty(TenantId) && !string.IsNullOrEmpty(UserObjectId);

    /// <summary>
    /// Validates that all required OBO fields are present and throws an exception if not.
    /// This method ensures tenant ID, user object ID, and serialized claims principal are all available
    /// before attempting OBO token acquisition.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when any required OBO fields are missing (TenantId, UserObjectId, or SerializedClaimsPrincipal).
    /// </exception>
    public void EnsureOboFields()
    {
        if (string.IsNullOrEmpty(TenantId))
        {
            throw new InvalidOperationException("TenantId is required for OBO token acquisition but is null or empty.");
        }

        if (string.IsNullOrEmpty(UserObjectId))
        {
            throw new InvalidOperationException("UserObjectId is required for OBO token acquisition but is null or empty.");
        }

        if (string.IsNullOrEmpty(SerializedClaimsPrincipal))
        {
            throw new InvalidOperationException("SerializedClaimsPrincipal is required for OBO token acquisition but is null or empty.");
        }
    }

    /// <summary>
    /// Attempts to deserialize the ClaimsPrincipal from the SerializedClaimsPrincipal property.
    /// </summary>
    /// <returns>The deserialized ClaimsPrincipal, or null if deserialization fails or no serialized data is available.</returns>
    public ClaimsPrincipal? DeserializeClaimsPrincipal()
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

    /// <summary>
    /// Creates a new McpUserContext from an AzMcpRequestContext.
    /// </summary>
    /// <typeparam name="TParams">The parameter type of the request context.</typeparam>
    /// <param name="requestContext">The request context to extract user information from.</param>
    /// <returns>A new McpUserContext instance.</returns>
    public static McpUserContext FromRequestContext<TParams>(Areas.Server.Commands.Runtime.AzMcpRequestContext<TParams> requestContext)
        where TParams : class
    {
        return new McpUserContext(
            claimsPrincipal: requestContext.GetClaimsPrincipal(),
            serializedClaimsPrincipal: requestContext.SerializedClaimsPrincipal,
            tenantId: requestContext.TenantId,
            userObjectId: requestContext.UserObjectId,
            sessionId: requestContext.SessionId);
    }
}