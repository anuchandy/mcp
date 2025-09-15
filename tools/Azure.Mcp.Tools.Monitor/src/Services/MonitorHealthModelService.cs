// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Nodes;
using Azure.Core;
using Azure.Mcp.Core.Areas.Server.Commands.Runtime;
using Azure.Mcp.Core.Options;
using Azure.Mcp.Core.Services.Azure;
using Azure.Mcp.Core.Services.Azure.Tenant;
using Azure.Mcp.Core.Services.Http;

namespace Azure.Mcp.Tools.Monitor.Services;

public class MonitorHealthModelService(ITenantService tenantService, IHttpClientService httpClientService)
    : BaseAzureService(tenantService), IMonitorHealthModelService
{
    private const int TokenExpirationBuffer = 300;
    private const string ManagementApiBaseUrl = "https://management.azure.com";
    private const string HealthModelsDataApiScope = "https://data.healthmodels.azure.com";
    private const string ApiVersion = "2023-10-01-preview";
    private readonly IHttpClientService _httpClientService = httpClientService;

    private string? _cachedDataplaneAccessToken;
    private string? _cachedControlPlaneAccessToken;
    private DateTimeOffset _dataplaneTokenExpiryTime;
    private DateTimeOffset _controlPlaneTokenExpiryTime;

    /// <summary>
    /// Retrieves the health information for a specific entity in a health model.
    /// </summary>
    /// <param name="context">User context for authentication and authorization</param>
    /// <param name="entity">The identifier of the entity whose health is being queried.</param>
    /// <param name="healthModelName">The name of the health model to query.</param>
    /// <param name="resourceGroupName">The name of the resource group containing the health model.</param>
    /// <param name="subscription">The Azure subscription ID containing the resource group.</param>
    /// <param name="authMethod">Optional. The authentication method to use for the request.</param>
    /// <param name="tenantId">Optional. The Azure tenant ID for authentication.</param>
    /// <param name="retryPolicy">Optional. Policy parameters for retrying failed requests.</param>
    /// <returns>A JSON node containing the entity's health information.</returns>
    /// <exception cref="ArgumentException">Thrown when required parameters are missing or invalid.</exception>
    /// <exception cref="Exception">Thrown when parsing the health response fails.</exception>
    public async Task<JsonNode> GetEntityHealth(
        McpUserContext context,
        string entity,
        string healthModelName,
        string resourceGroupName,
        string subscription,
        AuthMethod? authMethod = null,
        string? tenantId = null,
        RetryPolicyOptions? retryPolicy = null)
    {
        ValidateRequiredParameters(entity, healthModelName, resourceGroupName, subscription);

        string dataplaneEndpoint = await GetDataplaneEndpointAsync(context, subscription, resourceGroupName, healthModelName);
        string entityHealthUrl = $"{dataplaneEndpoint}api/entities/{entity}/history";

        string healthResponseString = await GetDataplaneResponseAsync(context, entityHealthUrl);
        return JsonNode.Parse(healthResponseString) ?? throw new Exception("Failed to parse health response to JSON.");
    }

    private async Task<string> GetDataplaneResponseAsync(McpUserContext context, string url)
    {
        string dataplaneToken = await GetDataplaneTokenAsync(context);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", dataplaneToken);

        HttpResponseMessage healthResponse = await _httpClientService.DefaultClient.SendAsync(request);
        healthResponse.EnsureSuccessStatusCode();

        string healthResponseString = await healthResponse.Content.ReadAsStringAsync();
        return healthResponseString;
    }

    private async Task<string> GetDataplaneEndpointAsync(McpUserContext context, string subscriptionId, string resourceGroupName, string healthModelName)
    {
        string token = await GetControlPlaneTokenAsync(context);
        string healthModelUrl = $"{ManagementApiBaseUrl}/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.CloudHealth/healthmodels/{healthModelName}?api-version={ApiVersion}";

        using var request = new HttpRequestMessage(HttpMethod.Get, healthModelUrl);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage response = await _httpClientService.DefaultClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        string responseString = await response.Content.ReadAsStringAsync();

        string dataplaneEndpoint = GetDataplaneEndpoint(responseString);
        return dataplaneEndpoint;
    }

    private static string GetDataplaneEndpoint(string jsonResponse)
    {
        try
        {
            JsonNode? json = JsonNode.Parse(jsonResponse);
            string? dataplaneEndpoint = json?["properties"]?["dataplaneEndpoint"]?.GetValue<string>();
            if (string.IsNullOrEmpty(dataplaneEndpoint))
            {
                throw new Exception("Dataplane endpoint is null or empty in the response.");
            }

            return dataplaneEndpoint!;
        }
        catch (Exception ex)
        {
            string errorMessage = $"Error parsing dataplane endpoint: {ex.Message}";
            throw new Exception(errorMessage, ex);
        }
    }

    private async Task<string> GetControlPlaneTokenAsync(McpUserContext context)
    {
        return await GetCachedTokenAsync(
            context,
            ManagementApiBaseUrl,
            () => _cachedControlPlaneAccessToken,
            (token) => _cachedControlPlaneAccessToken = token,
            () => _controlPlaneTokenExpiryTime,
            (expiry) => _controlPlaneTokenExpiryTime = expiry);
    }

    private async Task<string> GetDataplaneTokenAsync(McpUserContext context)
    {
        return await GetCachedTokenAsync(
            context,
            HealthModelsDataApiScope,
            () => _cachedDataplaneAccessToken,
            (token) => _cachedDataplaneAccessToken = token,
            () => _dataplaneTokenExpiryTime,
            (expiry) => _dataplaneTokenExpiryTime = expiry);
    }

    private async Task<string> GetCachedTokenAsync(
        McpUserContext context,
        string resource,
        Func<string?> getCachedToken,
        Action<string> setCachedToken,
        Func<DateTimeOffset> getExpiryTime,
        Action<DateTimeOffset> setExpiryTime)
    {
        var cachedToken = getCachedToken();
        if (cachedToken != null && DateTimeOffset.UtcNow < getExpiryTime())
        {
            return cachedToken;
        }

        AccessToken accessToken = await GetEntraIdAccessTokenAsync(context, resource);
        setCachedToken(accessToken.Token);
        setExpiryTime(accessToken.ExpiresOn.AddSeconds(-TokenExpirationBuffer));

        return getCachedToken()!;
    }

    private async Task<AccessToken> GetEntraIdAccessTokenAsync(McpUserContext context, string resource)
    {
        var tokenRequestContext = new TokenRequestContext(new[] { $"{resource}/.default" });
        var tokenCredential = await GetCredential(context);
        return await tokenCredential
            .GetTokenAsync(tokenRequestContext, CancellationToken.None)
            .ConfigureAwait(false);
    }
}
