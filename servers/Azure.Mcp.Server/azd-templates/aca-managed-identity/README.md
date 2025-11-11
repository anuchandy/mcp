# Azure MCP Server - ACA with Managed Identity

This reference Azure Developer CLI (azd) template shows how to host the server on Azure Container Apps with storage tools enabled, using managed identity authentication for secure access to Azure Storage.

## Prerequisites

- Azure subscription with **Owner** or **User Access Administrator** permissions
- [Azure Developer CLI (azd)](https://learn.microsoft.com/azure/developer/azure-developer-cli/install-azd)
- The list of Azure MCP Server tool areas (namespaces) you wish to enable (see [azmcp-commands.md](https://github.com/microsoft/mcp/blob/main/servers/Azure.Mcp.Server/docs/azmcp-commands.md)). This reference template uses the `storage` namespace

## Quick Start

This reference template deploys the Azure MCP Server with **read-only** Azure Storage tools enabled, accessible over HTTPS transport. For details on customizing server startup flags and configuration, see [Azure MCP Server documentation](https://github.com/microsoft/mcp/blob/main/servers/Azure.Mcp.Server/docs/azmcp-commands.md).

```bash
azd up
```

You'll be prompted for:
- **Storage Account Resource ID** - The Azure resource ID of the storage account the MCP server will access

## What Gets Deployed

- **Container App** - Runs Azure MCP Server with storage namespace
- **Role Assignments** - Container App managed identity granted roles for outbound authentication to the storage account specified by the input storage resource ID:
  - Reader (read-only access to storage account properties)
  - Storage Blob Data Reader (read-only access to blob data)

### Deployment Outputs

After deployment, retrieve `azd` outputs:

```bash
azd env get-values
```

Example output:
```
CONTAINER_APP_URL="https://azure-mcp-storage-server.wonderfulazmcp-a9561afd.eastus2.azurecontainerapps.io"
```

## Clean Up

```bash
azd down
```

## Template Structure

The `azd` template consists of the following Bicep modules:

- **`main.bicep`** - Orchestrates the deployment of all resources
- **`aca-infrastructure.bicep`** - Deploys Container App hosting the Azure MCP Server
- **`aca-role-assignment-resource-storage.bicep`** - Assigns Azure storage RBAC roles to the Container App managed identity on the storage account specified by the input storage resource ID

