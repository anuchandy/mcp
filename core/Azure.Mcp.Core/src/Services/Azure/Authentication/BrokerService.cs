// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Core;
using Microsoft.Extensions.Logging;

namespace Azure.Mcp.Core.Services.Azure.Authentication;

/// <summary>
/// Provides token brokering services using AzOBOTokenCredentials for inter-process communication.
/// This service enables OBO Child processes to request tokens from the OBO Parent via named pipes.
/// </summary>
public sealed class BrokerService : IBrokerService
{
    private readonly AzOBOTokenCredentials _credentials;
    private readonly ILogger<BrokerService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="BrokerService"/> class.
    /// </summary>
    /// <param name="credentials">The OBO token credentials for token acquisition.</param>
    /// <param name="logger">The logger for diagnostic information.</param>
    public BrokerService(AzOBOTokenCredentials credentials, ILogger<BrokerService> logger)
    {
        _credentials = credentials ?? throw new ArgumentNullException(nameof(credentials));
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
            var requestContext = new TokenRequestContext(scopes);
            var token = await _credentials.GetTokenAsync(requestContext, serializedClaimsPrincipal, cancellationToken);
            
            _logger.LogDebug("Successfully acquired token via broker service");
            return token;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to acquire token via broker service for scopes: {Scopes}", string.Join(", ", scopes));
            throw;
        }
    }
}