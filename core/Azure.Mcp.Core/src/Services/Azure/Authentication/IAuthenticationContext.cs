// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Mcp.Core.Services.Azure.Authentication;

/// <summary>
/// Provides authentication context information for the current request or session.
/// </summary>
/// <remarks>
/// This interface abstracts authentication details to support both traditional 
/// DefaultAzureCredential scenarios and On-Behalf-Of (OBO) authentication flows.
/// </remarks>
public interface IAuthenticationContext
{
    /// <summary>
    /// Gets the unique session identifier for the current user session.
    /// </summary>
    /// <value>
    /// A unique session identifier, or null if no session is active.
    /// In OBO scenarios, this is typically derived from user and tenant information.
    /// </value>
    string? SessionId { get; }

    /// <summary>
    /// Gets the unique identifier for the authenticated user.
    /// </summary>
    /// <value>
    /// The user's object ID from Azure AD, or null if not authenticated.
    /// </value>
    string? UserId { get; }

    /// <summary>
    /// Gets the tenant identifier for the authenticated user.
    /// </summary>
    /// <value>
    /// The Azure AD tenant ID, or null if not authenticated.
    /// </value>
    string? TenantId { get; }

    /// <summary>
    /// Gets a value indicating whether the current context represents an authenticated user.
    /// </summary>
    /// <value>
    /// true if the user is authenticated; otherwise, false.
    /// </value>
    bool IsAuthenticated { get; }
}
