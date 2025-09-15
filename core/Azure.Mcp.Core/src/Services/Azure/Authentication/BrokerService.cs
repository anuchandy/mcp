// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Security.Claims;
using Azure.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Web;

namespace Azure.Mcp.Core.Services.Azure.Authentication;

/// <summary>
/// Provides token brokering services using ITokenAcquisition for inter-process communication.
/// This service enables OBO Child processes to request tokens from the OBO Parent via named pipes.
/// </summary>
public sealed class BrokerService : IBrokerService
{
    private readonly ITokenAcquisition _tokenAcquisition;
    private readonly ILogger<BrokerService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="BrokerService"/> class.
    /// </summary>
    /// <param name="tokenAcquisition">The token acquisition service from Microsoft.Identity.Web.</param>
    /// <param name="logger">The logger for diagnostic information.</param>
    public BrokerService(ITokenAcquisition tokenAcquisition, ILogger<BrokerService> logger)
    {
        _tokenAcquisition = tokenAcquisition ?? throw new ArgumentNullException(nameof(tokenAcquisition));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<AccessToken> GetTokenAsync(string[] scopes, string serializedClaimsPrincipal, CancellationToken cancellationToken)
    {
        if (scopes == null || scopes.Length == 0)
        {
            throw new ArgumentException("Scopes cannot be null or empty.", nameof(scopes));
        }

        if (string.IsNullOrWhiteSpace(serializedClaimsPrincipal))
        {
            throw new ArgumentException("Serialized ClaimsPrincipal cannot be null or empty.", nameof(serializedClaimsPrincipal));
        }

        _logger.LogDebug("Acquiring token for {ScopeCount} scopes via broker service", scopes.Length);

        try
        {
            // Deserialize the ClaimsPrincipal from Base64
            var bytes = Convert.FromBase64String(serializedClaimsPrincipal);
            using var stream = new MemoryStream(bytes);
            using var reader = new BinaryReader(stream);
            var userPrincipal = new ClaimsPrincipal(reader);

            // Acquire token using Microsoft.Identity.Web OBO flow for specific user
            var accessToken = await _tokenAcquisition.GetAccessTokenForUserAsync(scopes, user: userPrincipal);

            // Parse the JWT to get expiration time
            var expiresOn = GetTokenExpiration(accessToken);
            
            _logger.LogDebug("Successfully acquired token via broker service");
            return new AccessToken(accessToken, expiresOn);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "Failed to acquire token via broker service for scopes: {Scopes}", string.Join(", ", scopes));
            throw new InvalidOperationException($"Failed to deserialize ClaimsPrincipal or acquire token via broker service: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to acquire token via broker service for scopes: {Scopes}", string.Join(", ", scopes));
            throw;
        }
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

            // If no expiration found, default to 1 hour
            return DateTimeOffset.UtcNow.AddHours(1);
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"Failed to parse JWT expiration: {ex.Message}", ex);
        }
    }
}