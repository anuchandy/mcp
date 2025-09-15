// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Mcp.Core.Areas.Server.Commands.Runtime;
using Azure.Mcp.Tools.BicepSchema.Services.ResourceProperties.Entities;

namespace Azure.Mcp.Tools.BicepSchema.Services
{
    public interface IBicepSchemaService
    {
        TypesDefinitionResult GetResourceTypeDefinitions(
        McpUserContext userContext,
        IServiceProvider serviceProvider,
        string resourceTypeName,
        string? apiVersion = null);
    }
}
