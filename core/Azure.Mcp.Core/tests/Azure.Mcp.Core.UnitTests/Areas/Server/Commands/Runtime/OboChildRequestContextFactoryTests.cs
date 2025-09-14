// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Azure.Mcp.Core.Areas.Server.Commands.Runtime;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using NSubstitute;
using Xunit;

namespace Azure.Mcp.Core.UnitTests.Areas.Server.Commands.Runtime;

public class OboChildRequestContextFactoryTests
{
    private static RequestContext<CallToolRequestParams> CreateCallToolRequest(Dictionary<string, JsonElement>? arguments = null)
    {
        var server = Substitute.For<IMcpServer>();
        return new RequestContext<CallToolRequestParams>(server)
        {
            Params = new CallToolRequestParams
            {
                Name = "test-tool",
                Arguments = arguments
            }
        };
    }

    private static OboChildRequestContextFactory CreateFactory()
    {
        var logger = Substitute.For<ILogger<OboChildRequestContextFactory>>();
        return new OboChildRequestContextFactory(logger);
    }

    [Fact]
    public void Create_WithTenantIdAndUserObjectIdInArguments_ExtractsIdentity()
    {
        // Arrange
        var arguments = new Dictionary<string, JsonElement>
        {
            ["tenantId"] = JsonDocument.Parse("\"tenant-123\"").RootElement,
            ["userObjectId"] = JsonDocument.Parse("\"user-456\"").RootElement,
            ["otherParam"] = JsonDocument.Parse("\"value\"").RootElement
        };

        var factory = CreateFactory();
        var request = CreateCallToolRequest(arguments);

        // Act
        var ctx = factory.Create(request);

        // Assert
        Assert.Equal("tenant-123", ctx.TenantId);
        Assert.Equal("user-456", ctx.UserObjectId);
        Assert.Equal(AzRuntimeMode.OboChild, ctx.Role);
        Assert.False(string.IsNullOrWhiteSpace(ctx.SessionId));
        Assert.False(string.IsNullOrWhiteSpace(ctx.CorrelationId));
    }

    [Fact]
    public void Create_WithNoIdentityArguments_DoesNotExtractIdentity()
    {
        // Arrange
        var arguments = new Dictionary<string, JsonElement>
        {
            ["someParam"] = JsonDocument.Parse("\"value\"").RootElement
        };

        var factory = CreateFactory();
        var request = CreateCallToolRequest(arguments);

        // Act
        var ctx = factory.Create(request);

        // Assert
        Assert.Null(ctx.TenantId);
        Assert.Null(ctx.UserObjectId);
        Assert.Equal(AzRuntimeMode.OboChild, ctx.Role);
    }

    [Fact]
    public void Create_WithListToolsRequest_DoesNotExtractIdentity()
    {
        // Arrange
        var server = Substitute.For<IMcpServer>();
        var request = new RequestContext<ListToolsRequestParams>(server)
        {
            Params = new ListToolsRequestParams()
        };

        var factory = CreateFactory();

        // Act
        var ctx = factory.Create(request);

        // Assert
        Assert.Null(ctx.TenantId);
        Assert.Null(ctx.UserObjectId);
        Assert.Equal(AzRuntimeMode.OboChild, ctx.Role);
    }

    [Fact]
    public void Create_NullRequest_Throws()
    {
        var factory = CreateFactory();
        Assert.Throws<ArgumentNullException>(() => factory.Create<CallToolRequestParams>(null!));
    }
}