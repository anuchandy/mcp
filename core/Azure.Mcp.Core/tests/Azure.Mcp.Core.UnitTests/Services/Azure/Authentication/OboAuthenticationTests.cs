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

public class OboCredentialFactoryTests
{
    [Fact]
    public void CreateCredentialForService_ReturnsCorrectCredential()
    {
        // Arrange
        var mockTokenAcquisition = Substitute.For<Microsoft.Identity.Web.ITokenAcquisition>();
        var factory = new OboCredentialFactory(mockTokenAcquisition);

        // Act
        var credential = factory.CreateCredentialForService(AzureService.ResourceManager);

        // Assert
        Assert.NotNull(credential);
        Assert.IsType<AzOBOTokenCredentials>(credential);
    }

    [Fact]
    public void CreateCredentialForScope_ReturnsCorrectCredential()
    {
        // Arrange
        var mockTokenAcquisition = Substitute.For<Microsoft.Identity.Web.ITokenAcquisition>();
        var factory = new OboCredentialFactory(mockTokenAcquisition);
        var customScope = "https://vault.azure.net/.default";

        // Act
        var credential = factory.CreateCredentialForScope(customScope);

        // Assert
        Assert.NotNull(credential);
        Assert.IsType<AzOBOTokenCredentials>(credential);
    }

    [Fact]
    public void CreateCredentialForScope_CachesCredentials()
    {
        // Arrange
        var mockTokenAcquisition = Substitute.For<Microsoft.Identity.Web.ITokenAcquisition>();
        var factory = new OboCredentialFactory(mockTokenAcquisition);
        var scope = "https://management.azure.com/.default";

        // Act
        var credential1 = factory.CreateCredentialForScope(scope);
        var credential2 = factory.CreateCredentialForScope(scope);

        // Assert
        Assert.Same(credential1, credential2); // Should be the same cached instance
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
            EnableOnBehalfOfAuth = true,
            EnableInsecureTransports = true
        };

        // Add required services for testing
        services.AddLogging();
        // Add mock ITokenAcquisition since OboCredentialFactory depends on it
        var mockTokenAcquisition = Substitute.For<Microsoft.Identity.Web.ITokenAcquisition>();
        services.AddScoped<Microsoft.Identity.Web.ITokenAcquisition>(_ => mockTokenAcquisition);

        // Act
        services.AddAzureMcpServer(serviceStartOptions);

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        
        // Verify that OBO services are registered when enabled
        var oboFactory = serviceProvider.GetService<IOboCredentialFactory>();
        var authContext = serviceProvider.GetService<IAuthenticationContext>();
        
        Assert.NotNull(oboFactory);
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
            EnableOnBehalfOfAuth = false,
            EnableInsecureTransports = true
        };

        // Add required services for testing
        services.AddLogging();

        // Act
        services.AddAzureMcpServer(serviceStartOptions);

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        
        // Verify that OBO services are not registered when disabled
        var oboFactory = serviceProvider.GetService<IOboCredentialFactory>();
        var authContext = serviceProvider.GetService<IAuthenticationContext>();
        
        Assert.Null(oboFactory);
        Assert.Null(authContext);  // Should not be registered when OBO is disabled
    }

    [Fact]
    public void AddAzureMcpServer_WithOboEnabledButStdioTransport_ThrowsInvalidOperationException()
    {
        // Arrange
        var services = new ServiceCollection();
        var serviceStartOptions = new ServiceStartOptions
        {
            EnableOnBehalfOfAuth = true,
            EnableInsecureTransports = false  // STDIO transport
        };

        services.AddLogging();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            services.AddAzureMcpServer(serviceStartOptions);
        });

        Assert.Contains("On-Behalf-Of authentication requires HTTP transport", exception.Message);
    }
}
