// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Mcp.Core.Areas.Server.Commands.Runtime;
using Azure.Mcp.Core.Areas.Server.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using NSubstitute;
using Xunit;

namespace Azure.Mcp.Core.UnitTests.Areas.Server.Commands.Runtime;

public class DefaultRequestContextFactoryTests
{
    private static RequestContext<ListToolsRequestParams> CreateListToolsRequest()
    {
        var server = Substitute.For<IMcpServer>();
        return new RequestContext<ListToolsRequestParams>(server)
        {
            Params = new ListToolsRequestParams()
        };
    }

    private static DefaultRequestContextFactory CreateFactory()
    {
        var logger = Substitute.For<ILogger<DefaultRequestContextFactory>>();
        return new DefaultRequestContextFactory(logger);
    }

    [Fact]
    public void Create_StandardMode_SetsParentRoleWithNoIdentity()
    {
        var factory = CreateFactory();
        var ctx = factory.Create(CreateListToolsRequest());
        
        // In standard mode, no user identity enrichment
        Assert.Null(ctx.TenantId);
        Assert.Null(ctx.UserObjectId);
        Assert.Equal(AzRuntimeMode.Default, ctx.Role);
        Assert.False(string.IsNullOrWhiteSpace(ctx.CorrelationId));
    }

    [Fact]
    public void Create_NullRequest_Throws()
    {
        var factory = CreateFactory();
        Assert.Throws<ArgumentNullException>(() => factory.Create<ListToolsRequestParams>(null!));
    }
}
