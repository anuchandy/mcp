// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using Azure.Core;
using Microsoft.Identity.Web;

namespace Azure.Mcp.Core.Services.Azure.Authentication;

/// <summary>
/// Factory for creating TokenCredential instances for On-Behalf-Of (OBO) authentication flows.
/// </summary>
/// <remarks>
/// This factory manages the creation and caching of TokenCredential instances for different
/// Azure services. Each service requires different OAuth 2.0 scopes, and this factory
/// handles the mapping from services to scopes and creates appropriate OBO credentials.
/// </remarks>
public class OboCredentialFactory : IOboCredentialFactory
{
    private readonly ITokenAcquisition _tokenAcquisition;
    private readonly ConcurrentDictionary<string, TokenCredential> _credentialCache;

    /// <summary>
    /// Initializes a new instance of the OboCredentialFactory class.
    /// </summary>
    /// <param name="tokenAcquisition">The token acquisition service from Microsoft.Identity.Web.</param>
    /// <exception cref="ArgumentNullException">Thrown when tokenAcquisition is null.</exception>
    public OboCredentialFactory(ITokenAcquisition tokenAcquisition)
    {
        _tokenAcquisition = tokenAcquisition ?? throw new ArgumentNullException(nameof(tokenAcquisition));
        _credentialCache = new ConcurrentDictionary<string, TokenCredential>();
    }

    /// <summary>
    /// Creates a TokenCredential for the specified Azure service.
    /// </summary>
    /// <param name="service">The Azure service for which to create a credential.</param>
    /// <returns>A TokenCredential configured for the specified service's scope.</returns>
    /// <remarks>
    /// This method maps the Azure service to its corresponding OAuth 2.0 scope and
    /// returns a cached TokenCredential instance for performance.
    /// </remarks>
    public TokenCredential CreateCredentialForService(AzureService service)
    {
        var scope = GetScopeForService(service);
        return CreateCredentialForScope(scope);
    }

    /// <summary>
    /// Creates a TokenCredential for a custom OAuth 2.0 scope.
    /// </summary>
    /// <param name="scope">The OAuth 2.0 scope for which to create a credential.</param>
    /// <returns>A TokenCredential configured for the specified scope.</returns>
    /// <exception cref="ArgumentNullException">Thrown when scope is null.</exception>
    /// <exception cref="ArgumentException">Thrown when scope is empty or whitespace.</exception>
    /// <remarks>
    /// This method creates and caches TokenCredential instances to avoid creating
    /// multiple credentials for the same scope.
    /// </remarks>
    public TokenCredential CreateCredentialForScope(string scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (string.IsNullOrWhiteSpace(scope))
        {
            throw new ArgumentException("Scope cannot be empty or whitespace.", nameof(scope));
        }

        return _credentialCache.GetOrAdd(scope, s => 
            new AzOBOTokenCredentials(_tokenAcquisition, s));
    }

    /// <summary>
    /// Maps Azure services to their corresponding OAuth 2.0 scopes.
    /// </summary>
    /// <param name="service">The Azure service to map.</param>
    /// <returns>The OAuth 2.0 scope for the specified service.</returns>
    /// <remarks>
    /// This method provides the mapping between Azure services and their required scopes.
    /// If a service is not explicitly mapped, it defaults to the Azure Resource Manager scope.
    /// </remarks>
    private static string GetScopeForService(AzureService service)
    {
        return service switch
        {
            AzureService.ResourceManager => "https://management.azure.com/.default",
            AzureService.StorageData => "https://storage.azure.com/.default",
            AzureService.KeyVaultData => "https://vault.azure.com/.default",
            AzureService.CosmosDb => "https://cosmos.azure.com/.default",
            AzureService.PostgreSQL => "https://ossrdbms-aad.database.windows.net/.default",
            AzureService.MySQL => "https://ossrdbms-aad.database.windows.net/.default",
            AzureService.ServiceBus => "https://servicebus.azure.net/.default",
            _ => "https://management.azure.com/.default" // Default fallback to ARM
        };
    }
}
