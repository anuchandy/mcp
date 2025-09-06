# On-Behalf-Of (OBO) Authentication Support

This is a prototype implementation of On-Behalf-Of authentication for Azure MCP Server. This feature allows the server to access Azure resources on behalf of authenticated users using their identity context.

## Overview

When enabled, the OBO authentication feature:

1. **Preserves User Identity**: Azure API calls are made using the authenticated user's identity rather than a shared service identity
2. **Maintains Backward Compatibility**: Existing deployments continue to work unchanged with `DefaultAzureCredential`
3. **Supports Multi-User Scenarios**: Each user's authentication context is isolated and secured
4. **Integrates with Microsoft.Identity.Web**: Leverages enterprise-grade token management and automatic refresh

## Configuration

### Enable OBO Authentication

Enable OBO authentication using command line flags:

```bash
# Start Azure MCP Server with OBO authentication
azmcp service start --enable-insecure-transports --enable-obo-auth

# Configure Azure AD settings via environment variables
export AzureAd__TenantId="your-tenant-id"
export AzureAd__ClientId="your-client-id" 
export AzureAd__ClientSecret="your-client-secret"

# Optional: Set ASPNETCORE_URLS for custom binding
export ASPNETCORE_URLS="http://localhost:8080"
```

Or via configuration:

```json
{
  "transport": "http",
  "enableInsecureTransports": true,
  "enableOnBehalfOfAuth": true
}
```

### Client Authentication

Clients connecting to the OBO-enabled server must include JWT Bearer tokens:

```bash
# Example HTTP request with authentication
curl -H "Authorization: Bearer <jwt-token>" \
     -H "Content-Type: application/json" \
     -d '{"method": "tools/list"}' \
     http://localhost:8080/mcp
```

### Automatic Service Registration

When OBO authentication is enabled with HTTP transport, the server automatically registers:

- `IOboCredentialFactory` - Creates OBO credentials for different Azure services
- `IAuthenticationContext` - HTTP-based authentication context using `HttpAuthenticationContext`
- `IHttpContextAccessor` - Required for accessing HTTP request context
- Microsoft.Identity.Web authentication middleware

**Important**: OBO authentication requires HTTP transport and will fail if attempted with STDIO transport.

## Authentication Context Implementations

### HttpAuthenticationContext

For HTTP-based scenarios with JWT Bearer tokens:

```csharp
public class HttpAuthenticationContext : IAuthenticationContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    
    public HttpAuthenticationContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }
    
    public string? SessionId => UserId + TenantId;
    public string? UserId => _httpContextAccessor.HttpContext?.User?.GetObjectId();
    public string? TenantId => _httpContextAccessor.HttpContext?.User?.GetTenantId();
    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true;
}
```

### DefaultAuthenticationContext

For development or single-user scenarios:

```csharp
services.AddScoped<IAuthenticationContext, DefaultAuthenticationContext>();
```

## How It Works

1. **Authentication Check**: `BaseAzureService.GetCredential()` checks for the presence of `IAuthenticationContext`
2. **OBO Credential Creation**: If authenticated user context exists, `IOboCredentialFactory` creates OBO-enabled credentials
3. **Fallback Behavior**: If no authentication context exists, falls back to `DefaultAzureCredential`
4. **Service-Specific Scopes**: The factory automatically maps Azure services to their required OAuth 2.0 scopes

## Supported Azure Services

The OBO implementation includes scope mappings for:

- Azure Resource Manager
- Azure Storage
- Azure Key Vault
- Microsoft Graph
- And other common Azure services

## Security Considerations

1. **Token Security**: User access tokens are handled securely by Microsoft.Identity.Web
2. **Scope Validation**: Only necessary scopes are requested for each Azure service
3. **Token Refresh**: Automatic token refresh prevents authentication interruptions
4. **Isolation**: Each user's token cache is isolated

## Backward Compatibility

Existing Azure MCP Server deployments continue to work without changes:

- `enableOnBehalfOfAuth` defaults to `false`
- When disabled, all authentication uses `DefaultAzureCredential`
- No breaking changes to existing APIs or configurations

**Note**: No manual service registration or custom ASP.NET Core setup is required. Azure.Mcp.Server automatically configures all necessary services when OBO authentication is enabled.
