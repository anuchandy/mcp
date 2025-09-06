// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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
/// Azure services on behalf of the authenticated user.
/// </remarks>
public class AzOBOTokenCredentials : TokenCredential
{
    private readonly ITokenAcquisition _tokenAcquisition;
    private readonly string[] _scopes;

    /// <summary>
    /// Initializes a new instance of the AzOBOTokenCredentials class.
    /// </summary>
    /// <param name="tokenAcquisition">The token acquisition service from Microsoft.Identity.Web.</param>
    /// <param name="scopes">The scopes to request for the target Azure service.</param>
    /// <exception cref="ArgumentNullException">Thrown when tokenAcquisition or scopes is null.</exception>
    /// <exception cref="ArgumentException">Thrown when scopes array is empty.</exception>
    public AzOBOTokenCredentials(ITokenAcquisition tokenAcquisition, string[] scopes)
    {
        _tokenAcquisition = tokenAcquisition ?? throw new ArgumentNullException(nameof(tokenAcquisition));
        _scopes = scopes ?? throw new ArgumentNullException(nameof(scopes));
        
        if (scopes.Length == 0)
        {
            throw new ArgumentException("At least one scope must be specified.", nameof(scopes));
        }
    }

    /// <summary>
    /// Convenience constructor for single scope scenarios.
    /// </summary>
    /// <param name="tokenAcquisition">The token acquisition service from Microsoft.Identity.Web.</param>
    /// <param name="scope">The scope to request for the target Azure service.</param>
    /// <exception cref="ArgumentNullException">Thrown when tokenAcquisition or scope is null.</exception>
    /// <exception cref="ArgumentException">Thrown when scope is empty or whitespace.</exception>
    public AzOBOTokenCredentials(ITokenAcquisition tokenAcquisition, string scope)
        : this(tokenAcquisition, new[] { scope ?? throw new ArgumentNullException(nameof(scope)) })
    {
        if (string.IsNullOrWhiteSpace(scope))
        {
            throw new ArgumentException("Scope cannot be null, empty, or whitespace.", nameof(scope));
        }
    }

    /// <summary>
    /// Acquires an access token for the specified scopes using the On-Behalf-Of flow.
    /// </summary>
    /// <param name="requestContext">The token request context containing scopes and other metadata.</param>
    /// <param name="cancellationToken">The cancellation token to observe.</param>
    /// <returns>An AccessToken containing the token and expiration information.</returns>
    /// <exception cref="InvalidOperationException">Thrown when token acquisition fails.</exception>
    public override async ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        try
        {
            // Use the scopes from the constructor, or fall back to request context scopes
            var scopesToUse = _scopes.Length > 0 ? _scopes : requestContext.Scopes.ToArray();
            
            // Acquire token using Microsoft.Identity.Web OBO flow
            var accessToken = await _tokenAcquisition.GetAccessTokenForUserAsync(scopesToUse);
            
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
                case 2: payload += "=="; break;
                case 3: payload += "="; break;
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
}
