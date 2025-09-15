// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Core;
using Azure.Identity;
using Azure.Mcp.Core.Services.Caching;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Web;

namespace Azure.Mcp.Core.Services.Azure.Authentication;

/// <summary>
/// OBO Parent implementation of IOboTokenCredentialFactory that creates token credentials
/// using direct token acquisition via ITokenAcquisition for authenticated users.
/// </summary>
/// <remarks>
/// This factory is used in OBO Parent scenarios where the process has direct access to
/// Microsoft.Identity.Web's ITokenAcquisition service and can perform OBO token flows
/// directly without requiring inter-process communication.
/// </remarks>
public sealed class OboTokenCredentialFactory : IOboTokenCredentialFactory
{
    private readonly ITokenAcquisition _tokenAcquisition;
    private readonly ICacheService _cacheService;
    private readonly ILogger<OboTokenCredentialFactory> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OboTokenCredentialFactory"/> class.
    /// </summary>
    /// <param name="tokenAcquisition">The token acquisition service for OBO token flows.</param>
    /// <param name="cacheService">The cache service for credential instances.</param>
    /// <param name="logger">The logger for diagnostic information.</param>
    public OboTokenCredentialFactory(
        ITokenAcquisition tokenAcquisition,
        ICacheService cacheService,
        ILogger<OboTokenCredentialFactory> logger)
    {
        _tokenAcquisition = tokenAcquisition ?? throw new ArgumentNullException(nameof(tokenAcquisition));
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public TokenCredential Get(McpUserContext userContext)
    {
        ArgumentNullException.ThrowIfNull(userContext);
        // Validate that all required OBO fields are present
        userContext.EnsureOboFields();

        if (!userContext.IsAuthenticated)
        {
            throw new ArgumentException("User context must represent an authenticated user.", nameof(userContext));
        }

        _logger.LogDebug("Creating new OBO token credential for tenant: {TenantId}, user: {UserObjectId}", 
            userContext.TenantId, userContext.UserObjectId);
        return new OboTokenCredential(_tokenAcquisition, userContext, _logger);
    }

    /// <summary>
    /// Private implementation of TokenCredential that uses ITokenAcquisition for OBO token flows.
    /// </summary>
    private sealed class OboTokenCredential : TokenCredential
    {
        private readonly ITokenAcquisition _tokenAcquisition;
        private readonly McpUserContext _userContext;
        private readonly ILogger _logger;

        public OboTokenCredential(
            ITokenAcquisition tokenAcquisition,
            McpUserContext userContext,
            ILogger logger)
        {
            _tokenAcquisition = tokenAcquisition;
            _userContext = userContext;
            _logger = logger;
        }

        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            return GetTokenAsync(requestContext, cancellationToken).GetAwaiter().GetResult();
        }

        public override async ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            if (requestContext.Scopes == null || requestContext.Scopes.Length == 0)
            {
                throw new ArgumentException("Token request context must have at least one scope.", nameof(requestContext));
            }

            _logger.LogDebug("Acquiring OBO token for {ScopeCount} scopes", requestContext.Scopes.Length);

            try
            {
                // Use the ClaimsPrincipal from the user context for OBO token acquisition
                var claimsPrincipal = _userContext.ClaimsPrincipal ?? _userContext.DeserializeClaimsPrincipal();
                if (claimsPrincipal == null)
                {
                    throw new InvalidOperationException("No ClaimsPrincipal available in user context for OBO token acquisition.");
                }

                var scopes = requestContext.Scopes.ToArray();
                var accessToken = await _tokenAcquisition.GetAccessTokenForUserAsync(scopes, user: claimsPrincipal);

                if (string.IsNullOrEmpty(accessToken))
                {
                    throw new InvalidOperationException("Token acquisition returned null or empty token.");
                }

                // Parse the JWT to get expiration time
                var expiresOn = GetTokenExpiration(accessToken);

                _logger.LogDebug("Successfully acquired OBO token via ITokenAcquisition");
                return new AccessToken(accessToken, expiresOn);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to acquire OBO token for scopes: {Scopes}", string.Join(", ", requestContext.Scopes));
                throw new AuthenticationFailedException($"Failed to acquire OBO token: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Extracts the expiration time from a JWT token.
        /// </summary>
        /// <param name="jwt">The JWT token to parse.</param>
        /// <returns>The expiration time as a DateTimeOffset.</returns>
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

                // Add padding if needed
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

                // If no expiration found, default to 1 hour
                return DateTimeOffset.UtcNow.AddHours(1);
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"Failed to parse JWT expiration: {ex.Message}", ex);
            }
        }
    }
}