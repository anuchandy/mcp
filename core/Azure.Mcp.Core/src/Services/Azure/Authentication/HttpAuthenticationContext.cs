// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Http;
using Microsoft.Identity.Web;

namespace Azure.Mcp.Core.Services.Azure.Authentication;

/// <summary>
/// HTTP-based implementation of IAuthenticationContext that extracts authentication
/// information from the current HTTP request context.
/// </summary>
/// <remarks>
/// This implementation is used in OBO scenarios where authentication information
/// is available through the HTTP request context. It leverages Microsoft.Identity.Web
/// extension methods to extract user and tenant information from JWT claims.
/// </remarks>
public class HttpAuthenticationContext : IAuthenticationContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of the HttpAuthenticationContext class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor to retrieve request information.</param>
    /// <exception cref="ArgumentNullException">Thrown when httpContextAccessor is null.</exception>
    public HttpAuthenticationContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    /// <summary>
    /// Gets the tenant identifier from the current user's claims.
    /// </summary>
    /// <value>
    /// The Azure AD tenant ID from the user's JWT token, or null if not available.
    /// </value>
    public string? TenantId => _httpContextAccessor.HttpContext?.User?.GetTenantId();

    /// <summary>
    /// Gets the user identifier from the current user's claims.
    /// </summary>
    /// <value>
    /// The user's object ID from the JWT token, or null if not available.
    /// </value>
    public string? UserId => _httpContextAccessor.HttpContext?.User?.GetObjectId();

    /// <summary>
    /// Gets a value indicating whether the current user is authenticated.
    /// </summary>
    /// <value>
    /// true if the user is authenticated; otherwise, false.
    /// </value>
    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;

    /// <summary>
    /// Gets a unique session identifier based on user and tenant information.
    /// </summary>
    /// <value>
    /// A session identifier derived from user ID and tenant ID, or null if not authenticated.
    /// </value>
    /// <remarks>
    /// The session ID is constructed by combining the user ID and tenant ID, providing
    /// a unique identifier for caching and session management purposes.
    /// </remarks>
    public string? SessionId => IsAuthenticated && !string.IsNullOrEmpty(UserId) && !string.IsNullOrEmpty(TenantId) 
        ? $"{UserId}_{TenantId}" 
        : null;
}
