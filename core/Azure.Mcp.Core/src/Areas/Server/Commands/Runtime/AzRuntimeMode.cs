// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Mcp.Core.Areas.Server.Commands.Runtime;

/// <summary>Runtime mode of the current azmcp process.</summary>
public enum AzRuntimeMode
{
    Default = 0,
    OboParent = 1,
    OboChild = 2,
}