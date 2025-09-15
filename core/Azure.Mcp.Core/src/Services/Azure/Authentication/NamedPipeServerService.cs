// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.IO.Pipes;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Core;
using Azure.Mcp.Core.Services.Azure.Authentication;
using Microsoft.Extensions.Logging;

namespace Azure.Mcp.Core.Services.Azure.Authentication;

/// <summary>
/// JSON serialization context for AOT compatibility.
/// </summary>
[JsonSerializable(typeof(TokenRequest))]
[JsonSerializable(typeof(TokenResponse))]
internal partial class BrokerJsonContext : JsonSerializerContext
{
}

/// <summary>
/// Named pipe server service that hosts the IBrokerService over named pipes.
/// Handles multiple client connections and provides token brokering services
/// for OBO Child processes requesting tokens from the OBO Parent.
/// </summary>
[SupportedOSPlatform("windows")]
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
public sealed class NamedPipeServerService
{
    private readonly IBrokerService _brokerService;
    private readonly ILogger<NamedPipeServerService> _logger;
    private readonly string _pipeName;

    /// <summary>
    /// Initializes a new instance of the <see cref="NamedPipeServerService"/> class.
    /// </summary>
    /// <param name="brokerService">The broker service for token acquisition.</param>
    /// <param name="logger">The logger for diagnostic information.</param>
    public NamedPipeServerService(IBrokerService brokerService, ILogger<NamedPipeServerService> logger)
    {
        _brokerService = brokerService ?? throw new ArgumentNullException(nameof(brokerService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _pipeName = $"azmcp_broker_{Environment.ProcessId}";
    }

    /// <summary>
    /// Gets the name of the named pipe for client connections.
    /// </summary>
    public string PipeName => _pipeName;

    /// <summary>
    /// Runs the named pipe server, accepting client connections and processing token requests.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting named pipe server on pipe: {PipeName}", _pipeName);

        var tasks = new List<Task>();

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // Use byte mode for cross-platform compatibility
                var server = new NamedPipeServerStream(_pipeName, PipeDirection.InOut, 10, PipeTransmissionMode.Byte);
                
                _logger.LogDebug("Waiting for client connection on pipe: {PipeName}", _pipeName);
                
                try
                {
                    await server.WaitForConnectionAsync(cancellationToken);
                    _logger.LogDebug("Client connected to pipe: {PipeName}", _pipeName);
                    
                    // Handle each client connection on a separate task
                    var clientTask = HandleClientAsync(server, cancellationToken);
                    tasks.Add(clientTask);
                    
                    // Clean up completed tasks to prevent memory leaks
                    tasks.RemoveAll(t => t.IsCompleted);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    server.Dispose();
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error waiting for client connection on pipe: {PipeName}", _pipeName);
                    server.Dispose();
                }
            }
        }
        finally
        {
            _logger.LogInformation("Waiting for all client connections to complete");
            await Task.WhenAll(tasks);
            _logger.LogInformation("Named pipe server stopped");
        }
    }

    /// <summary>
    /// Handles a single client connection, processing token requests and sending responses.
    /// </summary>
    /// <param name="server">The named pipe server stream for the client connection.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task HandleClientAsync(NamedPipeServerStream server, CancellationToken cancellationToken)
    {
        try
        {
            using (server)
            {
                while (server.IsConnected && !cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        // Read request
                        var request = await ReadTokenRequestAsync(server, cancellationToken);
                        if (request == null)
                        {
                            break; // Client disconnected
                        }

                        _logger.LogDebug("Received token request for {ScopeCount} scopes", request.Scopes?.Length ?? 0);

                        // Process request
                        var response = await ProcessTokenRequestAsync(request, cancellationToken);

                        // Send response
                        await WriteTokenResponseAsync(server, response, cancellationToken);
                        
                        _logger.LogDebug("Sent token response, success: {Success}", response.Success);
                    }
                    catch (Exception ex) when (!(ex is OperationCanceledException && cancellationToken.IsCancellationRequested))
                    {
                        _logger.LogError(ex, "Error processing client request");
                        
                        // Try to send error response
                        try
                        {
                            var errorResponse = new TokenResponse 
                            { 
                                Success = false, 
                                ErrorMessage = ex.Message 
                            };
                            await WriteTokenResponseAsync(server, errorResponse, cancellationToken);
                        }
                        catch (Exception sendEx)
                        {
                            _logger.LogError(sendEx, "Failed to send error response to client");
                        }
                        break;
                    }
                }
            }
        }
        catch (Exception ex) when (!(ex is OperationCanceledException && cancellationToken.IsCancellationRequested))
        {
            _logger.LogError(ex, "Error handling client connection");
        }
        finally
        {
            _logger.LogDebug("Client connection closed");
        }
    }

    /// <summary>
    /// Processes a token request using the broker service.
    /// </summary>
    /// <param name="request">The token request to process.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A token response containing the access token or error information.</returns>
    private async Task<TokenResponse> ProcessTokenRequestAsync(TokenRequest request, CancellationToken cancellationToken)
    {
        try
        {
            if (request.Scopes == null || request.Scopes.Length == 0)
            {
                return new TokenResponse 
                { 
                    Success = false, 
                    ErrorMessage = "Scopes are required" 
                };
            }

            if (string.IsNullOrWhiteSpace(request.SerializedClaimsPrincipal))
            {
                return new TokenResponse 
                { 
                    Success = false, 
                    ErrorMessage = "SerializedClaimsPrincipal is required" 
                };
            }

            var token = await _brokerService.GetTokenAsync(
                request.Scopes, 
                request.SerializedClaimsPrincipal, 
                cancellationToken);

            return new TokenResponse
            {
                Success = true,
                Token = token.Token,
                ExpiresOn = token.ExpiresOn
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to acquire token via broker service");
            return new TokenResponse 
            { 
                Success = false, 
                ErrorMessage = ex.Message 
            };
        }
    }

    /// <summary>
    /// Reads a token request from the named pipe stream with length prefix.
    /// </summary>
    /// <param name="stream">The named pipe stream to read from.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The deserialized token request, or null if the client disconnected.</returns>
    private static async Task<TokenRequest?> ReadTokenRequestAsync(NamedPipeServerStream stream, CancellationToken cancellationToken)
    {
        try
        {
            // Read length prefix (4 bytes)
            var lengthBuffer = new byte[4];
            var bytesRead = 0;
            while (bytesRead < 4)
            {
                var read = await stream.ReadAsync(lengthBuffer, bytesRead, 4 - bytesRead, cancellationToken);
                if (read == 0)
                {
                    return null; // Client disconnected
                }
                bytesRead += read;
            }

            var messageLength = BitConverter.ToInt32(lengthBuffer, 0);
            if (messageLength <= 0 || messageLength > 1024 * 1024) // 1MB limit
            {
                return null;
            }

            // Read message content
            var messageBuffer = new byte[messageLength];
            bytesRead = 0;
            while (bytesRead < messageLength)
            {
                var read = await stream.ReadAsync(messageBuffer, bytesRead, messageLength - bytesRead, cancellationToken);
                if (read == 0)
                {
                    return null; // Client disconnected
                }
                bytesRead += read;
            }

            var json = System.Text.Encoding.UTF8.GetString(messageBuffer);
            return JsonSerializer.Deserialize(json, BrokerJsonContext.Default.TokenRequest);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Writes a token response to the named pipe stream with length prefix.
    /// </summary>
    /// <param name="stream">The named pipe stream to write to.</param>
    /// <param name="response">The token response to serialize and write.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private static async Task WriteTokenResponseAsync(NamedPipeServerStream stream, TokenResponse response, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(response, BrokerJsonContext.Default.TokenResponse);
        var messageBuffer = System.Text.Encoding.UTF8.GetBytes(json);
        var lengthBuffer = BitConverter.GetBytes(messageBuffer.Length);

        // Write length prefix
        await stream.WriteAsync(lengthBuffer, 0, lengthBuffer.Length, cancellationToken);
        
        // Write message content
        await stream.WriteAsync(messageBuffer, 0, messageBuffer.Length, cancellationToken);
        
        await stream.FlushAsync(cancellationToken);
    }
}

/// <summary>
/// Represents a token request message sent from OBO Child to OBO Parent via named pipe.
/// </summary>
public sealed class TokenRequest
{
    /// <summary>
    /// Gets or sets the requested scopes for the token.
    /// </summary>
    public string[]? Scopes { get; set; }

    /// <summary>
    /// Gets or sets the Base64-encoded serialized ClaimsPrincipal representing the user identity.
    /// </summary>
    public string? SerializedClaimsPrincipal { get; set; }
}

/// <summary>
/// Represents a token response message sent from OBO Parent to OBO Child via named pipe.
/// </summary>
public sealed class TokenResponse
{
    /// <summary>
    /// Gets or sets a value indicating whether the token request was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the access token if the request was successful.
    /// </summary>
    public string? Token { get; set; }

    /// <summary>
    /// Gets or sets the token expiration time if the request was successful.
    /// </summary>
    public DateTimeOffset ExpiresOn { get; set; }

    /// <summary>
    /// Gets or sets the error message if the request failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}