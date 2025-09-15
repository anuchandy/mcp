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
        // Add mock ITokenAcquisition for OBO factory integration
        var mockTokenAcquisition = Substitute.For<Microsoft.Identity.Web.ITokenAcquisition>();
        services.AddScoped<Microsoft.Identity.Web.ITokenAcquisition>(_ => mockTokenAcquisition);

        // Act
        services.AddAzureMcpServer(serviceStartOptions);

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        
        // Verify that OBO services are registered when enabled
        var oboFactory = serviceProvider.GetService<IOboTokenCredentialFactory>();
        var authContext = serviceProvider.GetService<IAuthenticationContext>();
        
        Assert.NotNull(oboFactory);
        Assert.IsType<OboTokenCredentialFactory>(oboFactory);
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
        var oboFactory = serviceProvider.GetService<IOboTokenCredentialFactory>();
        var authContext = serviceProvider.GetService<IAuthenticationContext>();
        
        Assert.Null(oboFactory);
        Assert.Null(authContext);  // Should not be registered when OBO is disabled
    }
}
