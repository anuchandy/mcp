// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Security.Claims;
using Azure.Core;
using Azure.Mcp.Core.Services.Azure.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Web;
using NSubstitute;
using Xunit;

namespace Azure.Mcp.Core.UnitTests.Services.Azure.Authentication;

public class BrokerServiceTests
{
    [Fact]
    public void Constructor_WithValidParameters_Succeeds()
    {
        // Arrange
        var mockTokenAcquisition = Substitute.For<ITokenAcquisition>();
        var mockLogger = Substitute.For<ILogger<BrokerService>>();

        // Act
        var brokerService = new BrokerService(mockTokenAcquisition, mockLogger);

        // Assert
        Assert.NotNull(brokerService);
    }

    [Fact]
    public void Constructor_WithNullTokenAcquisition_ThrowsArgumentNullException()
    {
        // Arrange
        var mockLogger = Substitute.For<ILogger<BrokerService>>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new BrokerService(null!, mockLogger));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var mockTokenAcquisition = Substitute.For<ITokenAcquisition>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new BrokerService(mockTokenAcquisition, null!));
    }

    [Fact]
    public async Task GetTokenAsync_WithNullScopes_ThrowsArgumentException()
    {
        // Arrange
        var mockTokenAcquisition = Substitute.For<ITokenAcquisition>();
        var mockLogger = Substitute.For<ILogger<BrokerService>>();
        var brokerService = new BrokerService(mockTokenAcquisition, mockLogger);
        var serializedClaimsPrincipal = CreateSerializedClaimsPrincipal();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await brokerService.GetTokenAsync(null!, serializedClaimsPrincipal, CancellationToken.None));
    }

    [Fact]
    public async Task GetTokenAsync_WithEmptyScopes_ThrowsArgumentException()
    {
        // Arrange
        var mockTokenAcquisition = Substitute.For<ITokenAcquisition>();
        var mockLogger = Substitute.For<ILogger<BrokerService>>();
        var brokerService = new BrokerService(mockTokenAcquisition, mockLogger);
        var serializedClaimsPrincipal = CreateSerializedClaimsPrincipal();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await brokerService.GetTokenAsync(Array.Empty<string>(), serializedClaimsPrincipal, CancellationToken.None));
    }

    [Fact]
    public async Task GetTokenAsync_WithNullSerializedClaimsPrincipal_ThrowsArgumentException()
    {
        // Arrange
        var mockTokenAcquisition = Substitute.For<ITokenAcquisition>();
        var mockLogger = Substitute.For<ILogger<BrokerService>>();
        var brokerService = new BrokerService(mockTokenAcquisition, mockLogger);
        var scopes = new[] { "https://management.azure.com/.default" };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await brokerService.GetTokenAsync(scopes, null!, CancellationToken.None));
    }

    [Fact]
    public async Task GetTokenAsync_WithEmptySerializedClaimsPrincipal_ThrowsArgumentException()
    {
        // Arrange
        var mockTokenAcquisition = Substitute.For<ITokenAcquisition>();
        var mockLogger = Substitute.For<ILogger<BrokerService>>();
        var brokerService = new BrokerService(mockTokenAcquisition, mockLogger);
        var scopes = new[] { "https://management.azure.com/.default" };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await brokerService.GetTokenAsync(scopes, string.Empty, CancellationToken.None));
    }

    [Fact]
    public async Task GetTokenAsync_WithValidParameters_CallsTokenAcquisition()
    {
        // Arrange
        var mockTokenAcquisition = Substitute.For<ITokenAcquisition>();
        var mockLogger = Substitute.For<ILogger<BrokerService>>();
        var brokerService = new BrokerService(mockTokenAcquisition, mockLogger);
        
        var scopes = new[] { "https://management.azure.com/.default" };
        var serializedClaimsPrincipal = CreateSerializedClaimsPrincipal();
        
        // Create a valid JWT token with exp claim (expires in 1 hour)
        var exp = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
        var payload = $"{{\"exp\":{exp}}}";
        var payloadBase64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(payload));
        var expectedToken = $"eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.{payloadBase64}.signature";
        
        mockTokenAcquisition
            .GetAccessTokenForUserAsync(
                Arg.Is<string[]>(s => s.SequenceEqual(scopes)),
                user: Arg.Any<ClaimsPrincipal>())
            .Returns(Task.FromResult(expectedToken));

        // Act
        var result = await brokerService.GetTokenAsync(scopes, serializedClaimsPrincipal, CancellationToken.None);

        // Assert
        Assert.Equal(expectedToken, result.Token);
        Assert.True(result.ExpiresOn > DateTimeOffset.UtcNow);
        await mockTokenAcquisition.Received(1).GetAccessTokenForUserAsync(
            Arg.Is<string[]>(s => s.SequenceEqual(scopes)),
            user: Arg.Any<ClaimsPrincipal>());
    }

    [Fact]
    public async Task GetTokenAsync_WithMultipleScopes_PassesAllScopes()
    {
        // Arrange
        var mockTokenAcquisition = Substitute.For<ITokenAcquisition>();
        var mockLogger = Substitute.For<ILogger<BrokerService>>();
        var brokerService = new BrokerService(mockTokenAcquisition, mockLogger);
        
        var scopes = new[] { 
            "https://management.azure.com/.default",
            "https://vault.azure.com/.default",
            "https://storage.azure.com/.default"
        };
        var serializedClaimsPrincipal = CreateSerializedClaimsPrincipal();
        
        // Create a valid JWT token with exp claim
        var exp = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
        var payload = $"{{\"exp\":{exp}}}";
        var payloadBase64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(payload));
        var expectedToken = $"eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.{payloadBase64}.signature";
        
        mockTokenAcquisition
            .GetAccessTokenForUserAsync(
                Arg.Is<string[]>(s => s.SequenceEqual(scopes)),
                user: Arg.Any<ClaimsPrincipal>())
            .Returns(Task.FromResult(expectedToken));

        // Act
        var result = await brokerService.GetTokenAsync(scopes, serializedClaimsPrincipal, CancellationToken.None);

        // Assert
        Assert.Equal(expectedToken, result.Token);
        await mockTokenAcquisition.Received(1).GetAccessTokenForUserAsync(
            Arg.Is<string[]>(s => s.SequenceEqual(scopes)),
            user: Arg.Any<ClaimsPrincipal>());
    }

    [Fact]
    public async Task GetTokenAsync_WithInvalidSerializedClaimsPrincipal_ThrowsInvalidOperationException()
    {
        // Arrange
        var mockTokenAcquisition = Substitute.For<ITokenAcquisition>();
        var mockLogger = Substitute.For<ILogger<BrokerService>>();
        var brokerService = new BrokerService(mockTokenAcquisition, mockLogger);
        
        var scopes = new[] { "https://management.azure.com/.default" };
        var invalidSerializedClaimsPrincipal = "invalid-base64-string";

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await brokerService.GetTokenAsync(scopes, invalidSerializedClaimsPrincipal, CancellationToken.None));
    }

    [Fact]
    public async Task GetTokenAsync_WhenTokenAcquisitionFails_ThrowsInvalidOperationException()
    {
        // Arrange
        var mockTokenAcquisition = Substitute.For<ITokenAcquisition>();
        var mockLogger = Substitute.For<ILogger<BrokerService>>();
        var brokerService = new BrokerService(mockTokenAcquisition, mockLogger);
        
        var scopes = new[] { "https://management.azure.com/.default" };
        var serializedClaimsPrincipal = CreateSerializedClaimsPrincipal();
        
        mockTokenAcquisition
            .GetAccessTokenForUserAsync(Arg.Any<string[]>(), user: Arg.Any<ClaimsPrincipal>())
            .Returns(Task.FromException<string>(new Exception("Token acquisition failed")));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await brokerService.GetTokenAsync(scopes, serializedClaimsPrincipal, CancellationToken.None));
    }

    [Fact]
    public async Task GetTokenAsync_WithValidJwtToken_ParsesExpirationCorrectly()
    {
        // Arrange
        var mockTokenAcquisition = Substitute.For<ITokenAcquisition>();
        var mockLogger = Substitute.For<ILogger<BrokerService>>();
        var brokerService = new BrokerService(mockTokenAcquisition, mockLogger);
        
        var scopes = new[] { "https://management.azure.com/.default" };
        var serializedClaimsPrincipal = CreateSerializedClaimsPrincipal();
        
        // Create a JWT token with specific expiration time
        var expectedExpiration = DateTimeOffset.UtcNow.AddHours(2);
        var exp = expectedExpiration.ToUnixTimeSeconds();
        var payload = $"{{\"exp\":{exp}}}";
        var payloadBase64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(payload));
        var jwtToken = $"eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.{payloadBase64}.signature";
        
        mockTokenAcquisition
            .GetAccessTokenForUserAsync(Arg.Any<string[]>(), user: Arg.Any<ClaimsPrincipal>())
            .Returns(Task.FromResult(jwtToken));

        // Act
        var result = await brokerService.GetTokenAsync(scopes, serializedClaimsPrincipal, CancellationToken.None);

        // Assert
        Assert.Equal(jwtToken, result.Token);
        // Allow for small time differences in test execution
        Assert.True(Math.Abs((result.ExpiresOn - expectedExpiration).TotalSeconds) < 2);
    }

    [Fact]
    public async Task GetTokenAsync_WithJwtTokenWithoutExpClaim_UsesDefaultExpiration()
    {
        // Arrange
        var mockTokenAcquisition = Substitute.For<ITokenAcquisition>();
        var mockLogger = Substitute.For<ILogger<BrokerService>>();
        var brokerService = new BrokerService(mockTokenAcquisition, mockLogger);
        
        var scopes = new[] { "https://management.azure.com/.default" };
        var serializedClaimsPrincipal = CreateSerializedClaimsPrincipal();
        
        // Create a JWT token without exp claim
        var payload = "{\"sub\":\"user123\",\"name\":\"Test User\"}";
        var payloadBase64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(payload));
        var jwtToken = $"eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.{payloadBase64}.signature";
        
        mockTokenAcquisition
            .GetAccessTokenForUserAsync(Arg.Any<string[]>(), user: Arg.Any<ClaimsPrincipal>())
            .Returns(Task.FromResult(jwtToken));

        var beforeCall = DateTimeOffset.UtcNow;

        // Act
        var result = await brokerService.GetTokenAsync(scopes, serializedClaimsPrincipal, CancellationToken.None);

        var afterCall = DateTimeOffset.UtcNow;

        // Assert
        Assert.Equal(jwtToken, result.Token);
        // Should default to 1 hour from now
        Assert.True(result.ExpiresOn >= beforeCall.AddHours(1).AddSeconds(-1));
        Assert.True(result.ExpiresOn <= afterCall.AddHours(1).AddSeconds(1));
    }

    [Fact]
    public async Task GetTokenAsync_WithInvalidJwtFormat_ThrowsArgumentException()
    {
        // Arrange
        var mockTokenAcquisition = Substitute.For<ITokenAcquisition>();
        var mockLogger = Substitute.For<ILogger<BrokerService>>();
        var brokerService = new BrokerService(mockTokenAcquisition, mockLogger);
        
        var scopes = new[] { "https://management.azure.com/.default" };
        var serializedClaimsPrincipal = CreateSerializedClaimsPrincipal();
        
        // Return an invalid JWT format (missing parts)
        var invalidJwt = "invalid.jwt";
        
        mockTokenAcquisition
            .GetAccessTokenForUserAsync(Arg.Any<string[]>(), user: Arg.Any<ClaimsPrincipal>())
            .Returns(Task.FromResult(invalidJwt));

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await brokerService.GetTokenAsync(scopes, serializedClaimsPrincipal, CancellationToken.None));
    }

    [Fact]
    public async Task GetTokenAsync_WithCancellationToken_PassesToTokenAcquisition()
    {
        // Arrange
        var mockTokenAcquisition = Substitute.For<ITokenAcquisition>();
        var mockLogger = Substitute.For<ILogger<BrokerService>>();
        var brokerService = new BrokerService(mockTokenAcquisition, mockLogger);
        
        var scopes = new[] { "https://management.azure.com/.default" };
        var serializedClaimsPrincipal = CreateSerializedClaimsPrincipal();
        var cancellationToken = new CancellationToken(true); // Already cancelled
        
        mockTokenAcquisition
            .GetAccessTokenForUserAsync(Arg.Any<string[]>(), user: Arg.Any<ClaimsPrincipal>())
            .Returns(Task.FromException<string>(new OperationCanceledException()));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await brokerService.GetTokenAsync(scopes, serializedClaimsPrincipal, cancellationToken));
    }

    /// <summary>
    /// Helper method to create a serialized ClaimsPrincipal for testing
    /// </summary>
    private static string CreateSerializedClaimsPrincipal()
    {
        var claims = new[]
        {
            new Claim("oid", "user-object-id-123"),
            new Claim("tid", "tenant-id-456"),
            new Claim("name", "Test User"),
            new Claim("email", "test@example.com")
        };
        
        var identity = new ClaimsIdentity(claims, "Bearer");
        var principal = new ClaimsPrincipal(identity);
        
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        principal.WriteTo(writer);
        
        return Convert.ToBase64String(stream.ToArray());
    }
}