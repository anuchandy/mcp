// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Runtime.Versioning;
using Azure.Core;
using Azure.Identity;
using Azure.Mcp.Core.Areas.Server.Commands.Runtime;
using Azure.Mcp.Core.Areas.Server.Options;
using Azure.Mcp.Core.Services.Caching;
using Microsoft.Extensions.Logging;

namespace Azure.Mcp.Core.Services.Azure.Authentication;

/// <summary>
/// OBO Child implementation of IOboTokenCredentialFactory that creates token credentials
/// using named pipe communication to proxy token requests to the OBO Parent process.
/// </summary>
/// <remarks>
/// This factory is used in OBO Child scenarios where the process does not have direct access
/// to Microsoft.Identity.Web's ITokenAcquisition service and must communicate with the parent
/// process via named pipes to acquire tokens on behalf of users.
/// </remarks>
[SupportedOSPlatform("windows")]
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
public sealed class OboProxyTokenCredentialFactory : IOboTokenCredentialFactory
{
    private readonly NamedPipeClientService _pipeClient;
    private readonly ICacheService _cacheService;
    private readonly ILogger<OboProxyTokenCredentialFactory> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OboProxyTokenCredentialFactory"/> class.
    /// </summary>
    /// <param name="serviceStartOptions">The service start options containing OBO channel configuration.</param>
    /// <param name="cacheService">The cache service for credential instances.</param>
    /// <param name="logger">The logger for diagnostic information.</param>
    /// <param name="pipeClientLogger">The logger for the named pipe client service.</param>
    public OboProxyTokenCredentialFactory(
        ServiceStartOptions serviceStartOptions,
        ICacheService cacheService,
        ILogger<OboProxyTokenCredentialFactory> logger,
        ILogger<NamedPipeClientService> pipeClientLogger)
    {
        ArgumentNullException.ThrowIfNull(serviceStartOptions);
        ArgumentNullException.ThrowIfNull(cacheService);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(pipeClientLogger);

        var pipeName = serviceStartOptions.OboChannel 
            ?? throw new ArgumentException("OboChannel must be specified for OBO Child processes", nameof(serviceStartOptions));

        _pipeClient = new NamedPipeClientService(pipeName, pipeClientLogger);
        _cacheService = cacheService;
        _logger = logger;

        _logger.LogInformation("Initialized OBO proxy factory with pipe: {PipeName}", pipeName);
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

        _logger.LogDebug("Creating new OBO proxy credential for tenant: {TenantId}, user: {UserObjectId}", 
            userContext.TenantId, userContext.UserObjectId);
        return new OboProxyCredential(_pipeClient, userContext, _logger);
    }

    /// <summary>
    /// Private implementation of TokenCredential that uses named pipe communication for OBO token acquisition.
    /// This replaces the functionality of BrokerTokenCredential with a cleaner, factory-based approach.
    /// </summary>
    private sealed class OboProxyCredential : TokenCredential
    {
        private readonly NamedPipeClientService _pipeClient;
        private readonly McpUserContext _userContext;
        private readonly ILogger _logger;

        public OboProxyCredential(
            NamedPipeClientService pipeClient,
            McpUserContext userContext,
            ILogger logger)
        {
            _pipeClient = pipeClient;
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

            if (string.IsNullOrEmpty(_userContext.SerializedClaimsPrincipal))
            {
                throw new InvalidOperationException("SerializedClaimsPrincipal is required for proxy token acquisition.");
            }

            _logger.LogDebug("Requesting OBO token for {ScopeCount} scopes via named pipe proxy", requestContext.Scopes.Length);

            try
            {
                var request = new TokenRequest
                {
                    Scopes = requestContext.Scopes,
                    SerializedClaimsPrincipal = _userContext.SerializedClaimsPrincipal
                };

                var response = await _pipeClient.SendTokenRequestAsync(request, cancellationToken);

                if (!response.Success)
                {
                    var errorMessage = response.ErrorMessage ?? "Token request failed";
                    _logger.LogError("OBO proxy token request failed: {ErrorMessage}", errorMessage);
                    throw new AuthenticationFailedException(errorMessage);
                }

                if (string.IsNullOrEmpty(response.Token))
                {
                    var errorMessage = "Token response was successful but token is null or empty";
                    _logger.LogError(errorMessage);
                    throw new AuthenticationFailedException(errorMessage);
                }

                _logger.LogDebug("Successfully acquired OBO token via named pipe proxy");
                return new AccessToken(response.Token, response.ExpiresOn);
            }
            catch (AuthenticationFailedException)
            {
                throw; // Re-throw authentication exceptions as-is
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error acquiring OBO token via named pipe proxy for scopes: {Scopes}", string.Join(", ", requestContext.Scopes));
                throw new AuthenticationFailedException("Failed to acquire token via OBO proxy service", ex);
            }
        }
    }
}