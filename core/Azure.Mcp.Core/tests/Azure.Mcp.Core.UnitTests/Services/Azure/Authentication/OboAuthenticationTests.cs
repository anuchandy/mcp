// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Core;
using Azure.Mcp.Core.Areas.Server.Commands;
using Azure.Mcp.Core.Areas.Server.Options;
using Azure.Mcp.Core.Services.Azure.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Azure.Mcp.Core.UnitTests.Services.Azure.Authentication;

public class AzOBOTokenCredentialsTests
{
    [Fact]
    public void Constructor_WithValidTokenAcquisition_Succeeds()
    {
        // Arrange
        var mockTokenAcquisition = Substitute.For<Microsoft.Identity.Web.ITokenAcquisition>();

        // Act
        var credential = new AzOBOTokenCredentials(mockTokenAcquisition);

        // Assert
        Assert.NotNull(credential);
    }

    [Fact]
    public void Constructor_WithNullTokenAcquisition_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new AzOBOTokenCredentials(null!));
    }

    [Fact]
    public async Task GetTokenAsync_WithValidScopes_CallsTokenAcquisition()
    {
        // Arrange
        var mockTokenAcquisition = Substitute.For<Microsoft.Identity.Web.ITokenAcquisition>();
        // Create a valid JWT token with exp claim (expires in 1 hour)
        var exp = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
        var payload = $"{{\"exp\":{exp}}}";
        var payloadBase64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(payload));
        var expectedToken = $"eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.{payloadBase64}.signature";
        var scopes = new[] { "https://management.azure.com/.default" };
        
        mockTokenAcquisition
            .GetAccessTokenForUserAsync(Arg.Is<string[]>(s => s.SequenceEqual(scopes)))
            .Returns(Task.FromResult(expectedToken));

        var credential = new AzOBOTokenCredentials(mockTokenAcquisition);
        var requestContext = new TokenRequestContext(scopes);

        // Act
        var result = await credential.GetTokenAsync(requestContext, CancellationToken.None);

        // Assert
        Assert.Equal(expectedToken, result.Token);
        await mockTokenAcquisition.Received(1).GetAccessTokenForUserAsync(Arg.Is<string[]>(s => s.SequenceEqual(scopes)));
    }

    [Fact]
    public async Task GetTokenAsync_WithNoScopes_UsesDefaultScope()
    {
        // Arrange
        var mockTokenAcquisition = Substitute.For<Microsoft.Identity.Web.ITokenAcquisition>();
        // Create a valid JWT token with exp claim (expires in 1 hour)
        var exp = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
        var payload = $"{{\"exp\":{exp}}}";
        var payloadBase64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(payload));
        var expectedToken = $"eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.{payloadBase64}.signature";
        var defaultScopes = new[] { "https://management.azure.com/.default" };
        
        mockTokenAcquisition
            .GetAccessTokenForUserAsync(Arg.Is<string[]>(s => s.SequenceEqual(defaultScopes)))
            .Returns(Task.FromResult(expectedToken));

        var credential = new AzOBOTokenCredentials(mockTokenAcquisition);
        var requestContext = new TokenRequestContext(new string[0]); // Empty scopes

        // Act
        var result = await credential.GetTokenAsync(requestContext, CancellationToken.None);

        // Assert
        Assert.Equal(expectedToken, result.Token);
        await mockTokenAcquisition.Received(1).GetAccessTokenForUserAsync(Arg.Is<string[]>(s => s.SequenceEqual(defaultScopes)));
    }
}

public class AuthenticationContextTests
{
    [Fact]
    public void DefaultAuthenticationContext_ReturnsNotAuthenticated()
    {
        // Arrange
        var context = new DefaultAuthenticationContext();

        // Act & Assert
        Assert.False(context.IsAuthenticated);
        Assert.Null(context.UserId);
        Assert.Null(context.TenantId);
        Assert.Null(context.SessionId);
    }

    [Fact]
    public void HttpAuthenticationContext_WithoutHttpContext_ReturnsNotAuthenticated()
    {
        // Arrange
        var mockHttpContextAccessor = Substitute.For<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
        mockHttpContextAccessor.HttpContext.Returns((Microsoft.AspNetCore.Http.HttpContext?)null);
        
        var context = new HttpAuthenticationContext(mockHttpContextAccessor);

        // Act & Assert
        Assert.False(context.IsAuthenticated);
        Assert.Null(context.UserId);
        Assert.Null(context.TenantId);
        Assert.Null(context.SessionId);
    }
}

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddAzureMcpServer_WithOboEnabled_RegistersOboServices()
    {
        // Arrange
        var services = new ServiceCollection();
        var serviceStartOptions = new ServiceStartOptions
        {
            EnableOBO = true,
            EnableInsecureTransports = true
        };

        // Add required services for testing
        services.AddLogging();
        // Add mock ITokenAcquisition since AzOBOTokenCredentials depends on it
        var mockTokenAcquisition = Substitute.For<Microsoft.Identity.Web.ITokenAcquisition>();
        services.AddScoped<Microsoft.Identity.Web.ITokenAcquisition>(_ => mockTokenAcquisition);

        // Act
        services.AddAzureMcpServer(serviceStartOptions);

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        
        // Verify that OBO services are registered when enabled
        var tokenCredential = serviceProvider.GetService<TokenCredential>();
        var authContext = serviceProvider.GetService<IAuthenticationContext>();
        
        Assert.NotNull(tokenCredential);
        Assert.IsType<AzOBOTokenCredentials>(tokenCredential);
        Assert.NotNull(authContext);
        Assert.IsType<HttpAuthenticationContext>(authContext);
    }

    [Fact]
    public void AddAzureMcpServer_WithOboDisabled_DoesNotRegisterOboServices()
    {
        // Arrange
        var services = new ServiceCollection();
        var serviceStartOptions = new ServiceStartOptions
        {
            EnableOBO = false,
            EnableInsecureTransports = true
        };

        // Add required services for testing
        services.AddLogging();

        // Act
        services.AddAzureMcpServer(serviceStartOptions);

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        
        // Verify that OBO services are not registered when disabled
        var tokenCredential = serviceProvider.GetService<TokenCredential>();
        var authContext = serviceProvider.GetService<IAuthenticationContext>();
        
        Assert.Null(tokenCredential);
        Assert.Null(authContext);  // Should not be registered when OBO is disabled
    }
}
