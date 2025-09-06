// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Security.Claims;
using Azure.Mcp.Core.Areas.Server.Commands.Runtime;
using NSubstitute;
using Xunit;

namespace Azure.Mcp.Core.UnitTests.Areas.Server.Commands.Runtime;

public class McpUserContextTests
{
    [Fact]
    public void Constructor_WithAllParameters_SetsPropertiesCorrectly()
    {
        // Arrange
        const string tenantId = "tenant-123";
        const string userObjectId = "user-456";
        const string serializedClaimsPrincipal = "dGVzdC1jbGFpbXM="; // Base64 for "test-claims"
        const AzRuntimeMode role = AzRuntimeMode.OboParent;

        // Act
        var context = new McpUserContext(tenantId, userObjectId, serializedClaimsPrincipal, role);

        // Assert
        Assert.Equal(tenantId, context.TenantId);
        Assert.Equal(userObjectId, context.UserObjectId);
        Assert.Equal(serializedClaimsPrincipal, context.SerializedClaimsPrincipal);
        Assert.Equal(role, context.Role);
    }

    [Fact]
    public void Constructor_WithNullParameters_SetsPropertiesCorrectly()
    {
        // Act
        var context = new McpUserContext(null, null, null, AzRuntimeMode.Default);

        // Assert
        Assert.Null(context.TenantId);
        Assert.Null(context.UserObjectId);
        Assert.Null(context.SerializedClaimsPrincipal);
        Assert.Equal(AzRuntimeMode.Default, context.Role);
    }

    [Fact]
    public void Empty_ReturnsContextWithNullValues()
    {
        // Act
        var context = McpUserContext.Empty;

        // Assert
        Assert.Null(context.TenantId);
        Assert.Null(context.UserObjectId);
        Assert.Null(context.SerializedClaimsPrincipal);
        Assert.Equal(AzRuntimeMode.Default, context.Role);
    }

    [Fact]
    public void ClaimsPrincipal_WithNullSerializedClaimsPrincipal_ReturnsNull()
    {
        // Arrange
        var context = new McpUserContext("tenant-123", "user-456", null, AzRuntimeMode.OboParent);

        // Act
        var claimsPrincipal = context.ClaimsPrincipal;

        // Assert
        Assert.Null(claimsPrincipal);
    }

    [Fact]
    public void ClaimsPrincipal_WithEmptySerializedClaimsPrincipal_ReturnsNull()
    {
        // Arrange
        var context = new McpUserContext("tenant-123", "user-456", "", AzRuntimeMode.OboParent);

        // Act
        var claimsPrincipal = context.ClaimsPrincipal;

        // Assert
        Assert.Null(claimsPrincipal);
    }

    [Fact]
    public void ClaimsPrincipal_WithValidSerializedClaimsPrincipal_ReturnsClaimsPrincipal()
    {
        // Arrange
        // Create a test ClaimsPrincipal and serialize it
        var originalClaims = new[]
        {
            new Claim("oid", "user-456"),
            new Claim("tid", "tenant-123"),
            new Claim("name", "Test User")
        };
        var originalIdentity = new ClaimsIdentity(originalClaims, "test");
        var originalPrincipal = new ClaimsPrincipal(originalIdentity);

        // Serialize the ClaimsPrincipal
        string serializedClaimsPrincipal;
        using (var stream = new MemoryStream())
        {
            using var writer = new BinaryWriter(stream);
            originalPrincipal.WriteTo(writer);
            var bytes = stream.ToArray();
            serializedClaimsPrincipal = Convert.ToBase64String(bytes);
        }

        var context = new McpUserContext("tenant-123", "user-456", serializedClaimsPrincipal, AzRuntimeMode.OboParent);

        // Act
        var claimsPrincipal = context.ClaimsPrincipal;

        // Assert
        Assert.NotNull(claimsPrincipal);
        Assert.Equal("user-456", claimsPrincipal.FindFirst("oid")?.Value);
        Assert.Equal("tenant-123", claimsPrincipal.FindFirst("tid")?.Value);
        Assert.Equal("Test User", claimsPrincipal.FindFirst("name")?.Value);
    }

    [Fact]
    public void ClaimsPrincipal_WithInvalidSerializedClaimsPrincipal_ReturnsNull()
    {
        // Arrange
        var context = new McpUserContext("tenant-123", "user-456", "invalid-base64!", AzRuntimeMode.OboParent);

        // Act
        var claimsPrincipal = context.ClaimsPrincipal;

        // Assert
        Assert.Null(claimsPrincipal);
    }

    [Fact]
    public void ToString_ReturnsExpectedFormat()
    {
        // Arrange
        var context = new McpUserContext("tenant-123", "user-456", "dGVzdA==", AzRuntimeMode.OboParent);

        // Act
        var result = context.ToString();

        // Assert
        Assert.Contains("Role=OboParent", result);
        Assert.Contains("TenantId=tenant-123", result);
        Assert.Contains("UserObjectId=user-456", result);
        Assert.Contains("HasClaimsPrincipal=True", result);
    }

    [Fact]
    public void ToString_WithNullValues_ReturnsExpectedFormat()
    {
        // Arrange
        var context = McpUserContext.Empty;

        // Act
        var result = context.ToString();

        // Assert
        Assert.Contains("Role=Default", result);
        Assert.Contains("TenantId=null", result);
        Assert.Contains("UserObjectId=null", result);
        Assert.Contains("HasClaimsPrincipal=False", result);
    }

    [Fact]
    public void FromRequestContext_CreatesCorrectMcpUserContext()
    {
        // Arrange
        var mockServer = Substitute.For<ModelContextProtocol.Server.IMcpServer>();
        var requestContext = new ModelContextProtocol.Server.RequestContext<ModelContextProtocol.Protocol.CallToolRequestParams>(mockServer);
        var azMcpRequestContext = new AzMcpRequestContext<ModelContextProtocol.Protocol.CallToolRequestParams>(
            requestContext,
            "session-123",
            "correlation-456", 
            "tenant-789",
            "user-101112",
            AzRuntimeMode.OboChild,
            DateTimeOffset.UtcNow,
            "dGVzdA==");

        // Act
        var mcpUserContext = McpUserContext.FromRequestContext(azMcpRequestContext);

        // Assert
        Assert.Equal("tenant-789", mcpUserContext.TenantId);
        Assert.Equal("user-101112", mcpUserContext.UserObjectId);
        Assert.Equal("dGVzdA==", mcpUserContext.SerializedClaimsPrincipal);
        Assert.Equal(AzRuntimeMode.OboChild, mcpUserContext.Role);
    }
}