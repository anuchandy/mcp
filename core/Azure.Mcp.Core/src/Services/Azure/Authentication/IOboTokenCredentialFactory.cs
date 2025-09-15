// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Core;

namespace Azure.Mcp.Core.Services.Azure.Authentication;

/// <summary>
/// Factory interface for creating OBO (On-Behalf-Of) token credentials based on user context.
/// Provides an abstraction layer that allows different implementations for OBO Parent and Child scenarios.
/// </summary>
/// <remarks>
/// This factory pattern enables:
/// - OBO Parent: Direct token acquisition using ITokenAcquisition
/// - OBO Child: Proxy token acquisition via named pipes to parent process
/// - Clean separation of concerns and easier testing
/// - Consistent interface regardless of the underlying authentication mechanism
/// </remarks>
public interface IOboTokenCredentialFactory
{
    /// <summary>
    /// Creates a TokenCredential instance configured for the specified user context.
    /// </summary>
    /// <param name="userContext">The user context containing authentication information for OBO token acquisition.</param>
    /// <returns>A TokenCredential instance that can acquire tokens on behalf of the specified user.</returns>
    /// <exception cref="ArgumentNullException">Thrown when userContext is null.</exception>
    /// <exception cref="ArgumentException">Thrown when userContext does not contain sufficient authentication information.</exception>
    /// <remarks>
    /// The returned TokenCredential will use the user's identity from the provided context to acquire
    /// Azure tokens for the requested scopes. The implementation details vary based on whether this
    /// is an OBO Parent (direct acquisition) or OBO Child (proxy via named pipes) scenario.
    /// </remarks>
    TokenCredential Get(McpUserContext userContext);
}