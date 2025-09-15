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
    }

    /// <summary>
    /// Gets an empty McpUserContext for scenarios where no user identity is available.
    /// Typically used in Default runtime mode with DefaultAzureCredential.
    /// </summary>
    public static McpUserContext Empty => new(null, null, null, AzRuntimeMode.Default);

    /// <summary>
    /// Deserializes and returns the ClaimsPrincipal from SerializedClaimsPrincipal if available.
    /// </summary>
    /// <returns>
    /// The deserialized ClaimsPrincipal containing full user identity, or null if not available.
    /// </returns>
    /// <remarks>
    /// This method enables service classes to access the complete user identity for token acquisition
    /// without requiring direct access to HttpContext. The ClaimsPrincipal can be passed directly
    /// to Microsoft.Identity.Web's ITokenAcquisition.GetAccessTokenForUserAsync() method.
    /// </remarks>
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
    /// Indicates whether this context has sufficient information for On-Behalf-Of token flows.
    /// </summary>
    public bool SupportsOboFlow => HasUserIdentity && 
                                   (Role == AzRuntimeMode.OboParent || Role == AzRuntimeMode.OboChild);

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