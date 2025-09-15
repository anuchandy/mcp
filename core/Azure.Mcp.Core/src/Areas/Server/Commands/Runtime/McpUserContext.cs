// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Security.Claims;

namespace Azure.Mcp.Core.Areas.Server.Commands.Runtime;

/// <summary>
/// Represents the user identity context for MCP commands and service operations.
/// This type encapsulates all user-specific authentication information needed for
/// Azure service operations, providing a thread-safe alternative to HttpContext access.
/// </summary>
/// <remarks>
/// This class is designed to carry user identity information through the entire MCP
/// command execution pipeline, from initial HTTP request capture through to service
/// method invocation. It supports all MCP runtime modes:
/// - OBO Parent: Populated from HttpContext.User claims
/// - OBO Child: Populated from serialized ClaimsPrincipal received from parent process
/// - Default: Empty context for DefaultAzureCredential scenarios
/// </remarks>
public sealed class McpUserContext
{
    /// <summary>
    /// Azure AD tenant ID from the user's JWT token (e.g., from 'tid' claim).
    /// Used for tenant-specific Azure operations and multi-tenant scenarios.
    /// </summary>
    public string? TenantId { get; }

    /// <summary>
    /// Azure AD user object ID from the user's JWT token (e.g., from 'oid' claim).
    /// Uniquely identifies the user within the tenant for authorization decisions.
    /// </summary>
    public string? UserObjectId { get; }

    /// <summary>
    /// Base64-encoded serialized ClaimsPrincipal containing the full user identity.
    /// Enables optimal token caching by Microsoft.Identity.Web when passed to child processes.
    /// Contains all original JWT claims for comprehensive identity context.
    /// </summary>
    public string? SerializedClaimsPrincipal { get; }

    /// <summary>
    /// The MCP runtime mode indicating how this user context was populated.
    /// Determines the authentication flow and credential acquisition strategy.
    /// </summary>
    public AzRuntimeMode Role { get; }

    /// <summary>
    /// Lazy-loaded deserialized ClaimsPrincipal containing the full user identity.
    /// Null if no serialized claims principal was provided or deserialization failed.
    /// </summary>
    private readonly Lazy<ClaimsPrincipal?> _claimsPrincipal;

    /// <summary>
    /// Initializes a new instance of McpUserContext with the specified user identity information.
    /// </summary>
    /// <param name="tenantId">Azure AD tenant ID (optional).</param>
    /// <param name="userObjectId">Azure AD user object ID (optional).</param>
    /// <param name="serializedClaimsPrincipal">Base64-encoded serialized ClaimsPrincipal (optional).</param>
    /// <param name="role">The MCP runtime mode for this context.</param>
    public McpUserContext(
        string? tenantId,
        string? userObjectId,
        string? serializedClaimsPrincipal,
        AzRuntimeMode role)
    {
        TenantId = tenantId;
        UserObjectId = userObjectId;
        SerializedClaimsPrincipal = serializedClaimsPrincipal;
        Role = role;

        // Lazily deserialize the ClaimsPrincipal when first accessed
        _claimsPrincipal = new Lazy<ClaimsPrincipal?>(() => DeserializeClaimsPrincipal(serializedClaimsPrincipal));
    }

    /// <summary>
    /// Gets an empty McpUserContext for scenarios where no user identity is available.
    /// Typically used in Default runtime mode with DefaultAzureCredential.
    /// </summary>
    public static McpUserContext Empty => new(null, null, null, AzRuntimeMode.Default);

    /// <summary>
    /// Gets the deserialized ClaimsPrincipal from SerializedClaimsPrincipal if available.
    /// </summary>
    /// <value>
    /// The deserialized ClaimsPrincipal containing full user identity, or null if not available.
    /// </value>
    /// <remarks>
    /// This property enables service classes to access the complete user identity for token acquisition
    /// without requiring direct access to HttpContext. The ClaimsPrincipal can be passed directly
    /// to Microsoft.Identity.Web's ITokenAcquisition.GetAccessTokenForUserAsync() method.
    /// </remarks>
    public ClaimsPrincipal? ClaimsPrincipal => _claimsPrincipal.Value;

    /// <summary>
    /// Static helper method to deserialize a ClaimsPrincipal from a Base64-encoded string.
    /// </summary>
    /// <param name="serializedClaimsPrincipal">Base64-encoded serialized ClaimsPrincipal.</param>
    /// <returns>The deserialized ClaimsPrincipal, or null if deserialization fails or input is null/empty.</returns>
    private static ClaimsPrincipal? DeserializeClaimsPrincipal(string? serializedClaimsPrincipal)
    {
        if (string.IsNullOrEmpty(serializedClaimsPrincipal))
            return null;

        try
        {
            var bytes = Convert.FromBase64String(serializedClaimsPrincipal);
            using var stream = new MemoryStream(bytes);
            using var reader = new BinaryReader(stream);
            return new ClaimsPrincipal(reader);
        }
        catch
        {
            // If deserialization fails, return null to gracefully degrade
            // Services can fall back to using TenantId/UserObjectId or DefaultAzureCredential
            return null;
        }
    }

    /// <summary>
    /// Indicates whether this context contains any user identity information.
    /// </summary>
    public bool HasUserIdentity => !string.IsNullOrEmpty(TenantId) || 
                                   !string.IsNullOrEmpty(UserObjectId) || 
                                   !string.IsNullOrEmpty(SerializedClaimsPrincipal);

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
    /// Creates a new McpUserContext from an AzMcpRequestContext.
    /// </summary>
    /// <param name="requestContext">The MCP request context containing user identity.</param>
    /// <returns>A new McpUserContext with identity information from the request context.</returns>
    public static McpUserContext FromRequestContext<TParams>(AzMcpRequestContext<TParams> requestContext)
        where TParams : class
    {
        return new McpUserContext(
            requestContext.TenantId,
            requestContext.UserObjectId,
            requestContext.SerializedClaimsPrincipal,
            requestContext.Role);
    }

    /// <summary>
    /// Returns a string representation of this McpUserContext for debugging purposes.
    /// Does not include sensitive information like the full ClaimsPrincipal.
    /// </summary>
    public override string ToString()
    {
        var hasClaimsPrincipal = !string.IsNullOrEmpty(SerializedClaimsPrincipal);
        return $"McpUserContext(Role={Role}, TenantId={TenantId ?? "null"}, " +
               $"UserObjectId={UserObjectId ?? "null"}, HasClaimsPrincipal={hasClaimsPrincipal})";
    }
}