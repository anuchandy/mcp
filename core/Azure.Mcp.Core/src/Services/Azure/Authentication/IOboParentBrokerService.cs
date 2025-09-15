// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Core;

namespace Azure.Mcp.Core.Services.Azure.Authentication;

/// <summary>
/// Provides token brokering services for inter-process communication between OBO Parent and Child processes.
/// This service enables OBO Child processes to request tokens from the OBO Parent via named pipes.
/// </summary>
public interface IOboParentBrokerService
{
    /// <summary>
    /// Acquires an access token for the specified scopes and user identity.
    /// </summary>
    /// <param name="scopes">The requested scopes for the token.</param>
    /// <param name="serializedClaimsPrincipal">The Base64-encoded serialized ClaimsPrincipal representing the user identity.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>An access token for the specified scopes and user.</returns>
    /// <exception cref="AuthenticationFailedException">Thrown when token acquisition fails.</exception>
    /// <exception cref="ArgumentException">Thrown when the serialized ClaimsPrincipal is invalid.</exception>
    Task<AccessToken> GetTokenAsync(string[] scopes, string serializedClaimsPrincipal, CancellationToken cancellationToken);
}