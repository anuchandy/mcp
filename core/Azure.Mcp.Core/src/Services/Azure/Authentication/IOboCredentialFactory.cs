// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Core;

namespace Azure.Mcp.Core.Services.Azure.Authentication;

/// <summary>
/// Factory interface for creating TokenCredential instances for On-Behalf-Of (OBO) authentication.
/// </summary>
/// <remarks>
/// This factory creates TokenCredential instances that use the OBO flow to acquire tokens
/// for downstream Azure services on behalf of an authenticated user. Different Azure services
/// require different OAuth 2.0 scopes, and this factory manages the mapping between services
/// and their required scopes.
/// </remarks>
public interface IOboCredentialFactory
{
    /// <summary>
    /// Creates a TokenCredential for the specified Azure service.
    /// </summary>
    /// <param name="service">The Azure service for which to create a credential.</param>
    /// <returns>A TokenCredential configured for the specified service's scope.</returns>
    /// <remarks>
    /// This method maps the Azure service to its corresponding OAuth 2.0 scope and
    /// returns a TokenCredential that can acquire tokens for that scope using the OBO flow.
    /// </remarks>
    TokenCredential CreateCredentialForService(AzureService service);

    /// <summary>
    /// Creates a TokenCredential for a custom OAuth 2.0 scope.
    /// </summary>
    /// <param name="scope">The OAuth 2.0 scope for which to create a credential.</param>
    /// <returns>A TokenCredential configured for the specified scope.</returns>
    /// <remarks>
    /// This method allows for creating credentials for custom or service-specific scopes
    /// that may not be covered by the predefined AzureService enumeration.
    /// </remarks>
    TokenCredential CreateCredentialForScope(string scope);
}
