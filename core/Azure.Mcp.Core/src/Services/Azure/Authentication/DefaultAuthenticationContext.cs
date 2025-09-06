// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Mcp.Core.Services.Azure.Authentication;

/// <summary>
/// Default implementation of IAuthenticationContext that represents an unauthenticated state.
/// </summary>
/// <remarks>
/// This implementation is used when OBO authentication is not enabled, providing
/// a no-op authentication context that indicates no user is authenticated. This
/// maintains backward compatibility with existing DefaultAzureCredential flows.
/// </remarks>
public class DefaultAuthenticationContext : IAuthenticationContext
{
    /// <summary>
    /// Gets the session identifier. Always returns null for unauthenticated context.
    /// </summary>
    public string? SessionId => null;

    /// <summary>
    /// Gets the user identifier. Always returns null for unauthenticated context.
    /// </summary>
    public string? UserId => null;

    /// <summary>
    /// Gets the tenant identifier. Always returns null for unauthenticated context.
    /// </summary>
    public string? TenantId => null;

    /// <summary>
    /// Gets a value indicating whether the user is authenticated. Always returns false.
    /// </summary>
    public bool IsAuthenticated => false;
}
