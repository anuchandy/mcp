// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Runtime.Versioning;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Logging;

namespace Azure.Mcp.Core.Services.Azure.Authentication;

/// <summary>
/// TokenCredential implementation that connects to OBO Parent process via named pipe
/// to request tokens for OBO Child processes. This enables token brokering across
/// process boundaries in multi-process OBO scenarios.
/// </summary>
[SupportedOSPlatform("windows")]
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
public sealed class BrokerTokenCredential : TokenCredential
{
    private readonly NamedPipeClientService _pipeClient;
    private readonly ILogger<BrokerTokenCredential> _logger;
    private readonly string _serializedClaimsPrincipal;

    /// <summary>
    /// Initializes a new instance of the <see cref="BrokerTokenCredential"/> class.
    /// </summary>
    /// <param name="pipeClient">The named pipe client service for communication with parent process.</param>
    /// <param name="serializedClaimsPrincipal">The Base64-encoded serialized ClaimsPrincipal for the current user context.</param>
    /// <param name="logger">The logger for diagnostic information.</param>
    public BrokerTokenCredential(
        NamedPipeClientService pipeClient, 
        string serializedClaimsPrincipal,
        ILogger<BrokerTokenCredential> logger)
    {
        _pipeClient = pipeClient ?? throw new ArgumentNullException(nameof(pipeClient));
        _serializedClaimsPrincipal = serializedClaimsPrincipal ?? throw new ArgumentNullException(nameof(serializedClaimsPrincipal));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (string.IsNullOrWhiteSpace(_serializedClaimsPrincipal))
        {
            throw new ArgumentException("SerializedClaimsPrincipal cannot be null or empty.", nameof(serializedClaimsPrincipal));
        }
    }

    /// <inheritdoc />
    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        return GetTokenAsync(requestContext, cancellationToken).GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public override async ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        if (requestContext.Scopes == null || requestContext.Scopes.Length == 0)
        {
            throw new ArgumentException("Token request context must have at least one scope.", nameof(requestContext));
        }

        _logger.LogDebug("Requesting token for {ScopeCount} scopes via broker client", requestContext.Scopes.Length);

        try
        {
            var request = new TokenRequest
            {
                Scopes = requestContext.Scopes,
                SerializedClaimsPrincipal = _serializedClaimsPrincipal
            };

            var response = await _pipeClient.SendTokenRequestAsync(request, cancellationToken);

            if (!response.Success)
            {
                var errorMessage = response.ErrorMessage ?? "Token request failed";
                _logger.LogError("Token request failed: {ErrorMessage}", errorMessage);
                throw new AuthenticationFailedException(errorMessage);
            }

            if (string.IsNullOrEmpty(response.Token))
            {
                var errorMessage = "Token response was successful but token is null or empty";
                _logger.LogError(errorMessage);
                throw new AuthenticationFailedException(errorMessage);
            }

            _logger.LogDebug("Successfully acquired token via broker client");
            return new AccessToken(response.Token, response.ExpiresOn);
        }
        catch (AuthenticationFailedException)
        {
            throw; // Re-throw authentication exceptions as-is
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error acquiring token via broker client");
            throw new AuthenticationFailedException("Failed to acquire token via broker service", ex);
        }
    }
}