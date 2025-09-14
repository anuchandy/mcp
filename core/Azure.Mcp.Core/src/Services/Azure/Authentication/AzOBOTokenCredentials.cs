// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Security.Claims;
using Azure.Core;
using Microsoft.Identity.Web;

namespace Azure.Mcp.Core.Services.Azure.Authentication;

/// <summary>
/// Azure TokenCredential implementation that uses Microsoft.Identity.Web's ITokenAcquisition
/// to perform On-Behalf-Of (OBO) token acquisition for Azure services.
/// </summary>
/// <remarks>
/// This class bridges Microsoft.Identity.Web with Azure SDK clients by implementing
/// the TokenCredential interface. It uses the OBO flow to acquire tokens for downstream
/// Azure services on behalf of the authenticated user. The credential is scope-aware and
/// will request tokens for the specific scopes provided in each TokenRequestContext.
/// </remarks>
public class AzOBOTokenCredentials : TokenCredential
{
    private readonly ITokenAcquisition _tokenAcquisition;

    /// <summary>
    /// Initializes a new instance of the AzOBOTokenCredentials class.
    /// </summary>
    /// <param name="tokenAcquisition">The token acquisition service from Microsoft.Identity.Web.</param>
    /// <exception cref="ArgumentNullException">Thrown when tokenAcquisition is null.</exception>
    public AzOBOTokenCredentials(ITokenAcquisition tokenAcquisition)
    {
        _tokenAcquisition = tokenAcquisition ?? throw new ArgumentNullException(nameof(tokenAcquisition));
    }

    /// <summary>
    /// Acquires an access token for the specified scopes using the On-Behalf-Of flow.
    /// </summary>
    /// <param name="requestContext">The token request context containing scopes and other metadata.</param>
    /// <param name="cancellationToken">The cancellation token to observe.</param>
    /// <returns>An AccessToken containing the token and expiration information.</returns>
    /// <exception cref="ArgumentException">Thrown when no scopes are provided in the request context.</exception>
    /// <exception cref="InvalidOperationException">Thrown when token acquisition fails.</exception>
    /// <remarks>
    /// This method is called within the scope of an HTTP request (each MCP tool call is a separate HTTP POST).
    /// The underlying ITokenAcquisition service automatically retrieves the current user's authentication
    /// context from the HttpContext via HttpContextAccessor to perform the OBO flow for the correct user.
    /// </remarks>
    public override async ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        try
        {
            // Use the scopes from the request context - this makes the credential scope-aware
            var scopes = requestContext.Scopes?.ToArray();
            if (scopes == null || scopes.Length == 0)
            {
                // Default to Azure Resource Manager scope if no scopes provided
                scopes = new[] { "https://management.azure.com/.default" };
            }

            // Acquire token using Microsoft.Identity.Web OBO flow
            var accessToken = await _tokenAcquisition.GetAccessTokenForUserAsync(scopes);

            // Parse the JWT to get expiration time
            var expiresOn = GetTokenExpiration(accessToken);

            return new AccessToken(accessToken, expiresOn);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to acquire token via OBO flow: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Acquires an access token for the specified scopes and user using the On-Behalf-Of flow.
    /// This overload is intended for broker services that need to acquire tokens on behalf of specific users.
    /// </summary>
    /// <param name="requestContext">The token request context containing scopes and other metadata.</param>
    /// <param name="userPrincipal">The ClaimsPrincipal of the user to acquire the token for.</param>
    /// <param name="cancellationToken">The cancellation token to observe.</param>
    /// <returns>An AccessToken containing the token and expiration information.</returns>
    /// <exception cref="ArgumentException">Thrown when no scopes are provided in the request context or userPrincipal is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when token acquisition fails.</exception>
    /// <remarks>
    /// This method is designed for OBO Child scenarios where a broker service needs to acquire tokens
    /// for specific users identified by their ClaimsPrincipal. This is typically used in multi-tenant
    /// broker architectures where the broker handles token requests from child MCP servers.
    /// The ClaimsPrincipal should contain the necessary claims (like 'oid', 'tid') from the user's JWT token.
    /// </remarks>
    public async ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, ClaimsPrincipal userPrincipal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(userPrincipal);

        try
        {
            // Use the scopes from the request context - this makes the credential scope-aware
            var scopes = requestContext.Scopes?.ToArray();
            if (scopes == null || scopes.Length == 0)
            {
                // Default to Azure Resource Manager scope if no scopes provided
                scopes = new[] { "https://management.azure.com/.default" };
            }

            // Acquire token using Microsoft.Identity.Web OBO flow for specific user
            var accessToken = await _tokenAcquisition.GetAccessTokenForUserAsync(scopes, user: userPrincipal);

            // Parse the JWT to get expiration time
            var expiresOn = GetTokenExpiration(accessToken);

            return new AccessToken(accessToken, expiresOn);
        }
        catch (Exception ex)
        {
            var userId = userPrincipal.GetObjectId() ?? "unknown";
            throw new InvalidOperationException($"Failed to acquire token via OBO flow for user '{userId}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Acquires an access token for the specified scopes and user using the On-Behalf-Of flow.
    /// This overload deserializes a Base64-encoded ClaimsPrincipal and uses it for optimal token caching.
    /// </summary>
    /// <param name="requestContext">The token request context containing scopes and other metadata.</param>
    /// <param name="serializedClaimsPrincipal">The Base64-encoded serialized ClaimsPrincipal from the authenticated user.</param>
    /// <param name="cancellationToken">The cancellation token to observe.</param>
    /// <returns>An AccessToken containing the token and expiration information.</returns>
    /// <exception cref="ArgumentException">Thrown when no scopes are provided in the request context or serializedClaimsPrincipal is null/invalid.</exception>
    /// <exception cref="InvalidOperationException">Thrown when token acquisition fails or ClaimsPrincipal deserialization fails.</exception>
    /// <remarks>
    /// This method is designed for OBO Child scenarios where the full ClaimsPrincipal has been serialized
    /// by the parent process and passed to the child process for optimal token caching. The serialized
    /// ClaimsPrincipal contains all claims from the user's JWT token, enabling Microsoft.Identity.Web
    /// to perform more efficient token cache lookups compared to using just user object ID and tenant ID.
    /// </remarks>
    public async ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, string serializedClaimsPrincipal, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(serializedClaimsPrincipal))
            throw new ArgumentException("Serialized ClaimsPrincipal cannot be null or empty", nameof(serializedClaimsPrincipal));

        try
        {
            // Deserialize the ClaimsPrincipal from Base64
            var bytes = Convert.FromBase64String(serializedClaimsPrincipal);
            using var stream = new MemoryStream(bytes);
            using var reader = new BinaryReader(stream);
            var userPrincipal = new ClaimsPrincipal(reader);

            // Use the ClaimsPrincipal overload for optimal token caching
            return await GetTokenAsync(requestContext, userPrincipal, cancellationToken);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException($"Failed to deserialize ClaimsPrincipal or acquire token: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Synchronous version of GetTokenAsync. 
    /// Note: This will block the calling thread and is not recommended for async contexts.
    /// </summary>
    /// <param name="requestContext">The token request context containing scopes and other metadata.</param>
    /// <param name="cancellationToken">The cancellation token to observe.</param>
    /// <returns>An AccessToken containing the token and expiration information.</returns>
    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        // Use ConfigureAwait(false) to avoid deadlocks in sync contexts
        return GetTokenAsync(requestContext, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Extracts the expiration time from a JWT token.
    /// </summary>
    /// <param name="jwt">The JWT token to parse.</param>
    /// <returns>The expiration time as a DateTimeOffset.</returns>
    /// <exception cref="ArgumentException">Thrown when the JWT is invalid or missing expiration claim.</exception>
    private static DateTimeOffset GetTokenExpiration(string jwt)
    {
        try
        {
            var parts = jwt.Split('.');
            if (parts.Length < 2)
            {
                throw new ArgumentException("Invalid JWT format");
            }

            string payload = parts[1];

            switch (payload.Length % 4)
            {
                case 2:
                    payload += "==";
                    break;
                case 3:
                    payload += "=";
                    break;
            }

            var payloadBytes = Convert.FromBase64String(payload);
            var payloadJson = System.Text.Encoding.UTF8.GetString(payloadBytes);

            using var document = System.Text.Json.JsonDocument.Parse(payloadJson);

            if (document.RootElement.TryGetProperty("exp", out var expElement))
            {
                var expUnix = expElement.GetInt64();
                return DateTimeOffset.FromUnixTimeSeconds(expUnix);
            }

            // If no expiration found, default to 1 hour?
            return DateTimeOffset.UtcNow.AddHours(1);
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"Failed to parse JWT expiration: {ex.Message}", ex);
        }
    }

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
    
    public enum AzureService
    {
        /// <summary>
        /// Azure Resource Manager (management plane operations).
        /// Scope: https://management.azure.com/.default
        /// </summary>
        /// <remarks>
        /// Used for Azure Resource Manager operations such as managing subscriptions,
        /// resource groups, and resource providers.
        /// </remarks>
        ResourceManager,

        /// <summary>
        /// Azure Storage (data plane operations).
        /// Scope: https://storage.azure.com/.default
        /// </summary>
        /// <remarks>
        /// Used for Azure Storage data plane operations such as blob, table, queue,
        /// and file share access.
        /// </remarks>
        StorageData,

        /// <summary>
        /// Azure Key Vault (data plane operations).
        /// Scope: https://vault.azure.com/.default
        /// </summary>
        /// <remarks>
        /// Used for Azure Key Vault data plane operations such as accessing secrets,
        /// keys, and certificates.
        /// </remarks>
        KeyVaultData,

        /// <summary>
        /// Azure Cosmos DB (data plane operations).
        /// Scope: https://cosmos.azure.com/.default
        /// </summary>
        /// <remarks>
        /// Used for Azure Cosmos DB data plane operations such as database and
        /// container management.
        /// </remarks>
        CosmosDb,

        /// <summary>
        /// Azure Database for PostgreSQL.
        /// Scope: https://ossrdbms-aad.database.windows.net/.default
        /// </summary>
        /// <remarks>
        /// Used for Azure Database for PostgreSQL authentication.
        /// </remarks>
        PostgreSQL,

        /// <summary>
        /// Azure Database for MySQL.
        /// Scope: https://ossrdbms-aad.database.windows.net/.default
        /// </summary>
        /// <remarks>
        /// Used for Azure Database for MySQL authentication.
        /// </remarks>
        MySQL,

        /// <summary>
        /// Azure Service Bus.
        /// Scope: https://servicebus.azure.net/.default
        /// </summary>
        /// <remarks>
        /// Used for Azure Service Bus operations such as sending and receiving messages.
        /// </remarks>
        ServiceBus
    }
}
