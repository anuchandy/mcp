// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.IO.Pipes;
using System.Runtime.Versioning;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Azure.Mcp.Core.Services.Azure.Authentication;

/// <summary>
/// Named pipe client service that communicates with the OBO Parent process
/// to request tokens via the broker service. Handles connection management,
/// retry logic, and error handling for reliable inter-process communication.
/// </summary>
[SupportedOSPlatform("windows")]
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
public sealed class NamedPipeClientService
{
    private readonly ILogger<NamedPipeClientService> _logger;
    private readonly string _pipeName;
    private readonly TimeSpan _connectionTimeout;
    private readonly int _maxRetryAttempts;

    /// <summary>
    /// Initializes a new instance of the <see cref="NamedPipeClientService"/> class.
    /// </summary>
    /// <param name="parentProcessId">The process ID of the OBO Parent process hosting the broker service.</param>
    /// <param name="logger">The logger for diagnostic information.</param>
    public NamedPipeClientService(int parentProcessId, ILogger<NamedPipeClientService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _pipeName = $"azmcp_broker_{parentProcessId}";
        _connectionTimeout = TimeSpan.FromSeconds(10);
        _maxRetryAttempts = 3;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NamedPipeClientService"/> class with custom settings.
    /// </summary>
    /// <param name="parentProcessId">The process ID of the OBO Parent process hosting the broker service.</param>
    /// <param name="connectionTimeout">The timeout for establishing pipe connections.</param>
    /// <param name="maxRetryAttempts">The maximum number of retry attempts for failed requests.</param>
    /// <param name="logger">The logger for diagnostic information.</param>
    public NamedPipeClientService(
        int parentProcessId, 
        TimeSpan connectionTimeout, 
        int maxRetryAttempts, 
        ILogger<NamedPipeClientService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _pipeName = $"azmcp_broker_{parentProcessId}";
        _connectionTimeout = connectionTimeout;
        _maxRetryAttempts = Math.Max(1, maxRetryAttempts);
    }

    /// <summary>
    /// Sends a token request to the OBO Parent process and returns the response.
    /// </summary>
    /// <param name="request">The token request to send.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The token response from the OBO Parent process.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the pipe communication fails after all retry attempts.</exception>
    public async Task<TokenResponse> SendTokenRequestAsync(TokenRequest request, CancellationToken cancellationToken)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var attempt = 0;
        Exception? lastException = null;

        while (attempt < _maxRetryAttempts && !cancellationToken.IsCancellationRequested)
        {
            attempt++;
            
            try
            {
                _logger.LogDebug("Attempting to connect to broker pipe: {PipeName} (attempt {Attempt}/{MaxAttempts})", 
                    _pipeName, attempt, _maxRetryAttempts);

                using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut);
                
                // Connect with timeout
                var connectTask = client.ConnectAsync(_connectionTimeout, cancellationToken);
                await connectTask;

                if (!client.IsConnected)
                {
                    throw new InvalidOperationException($"Failed to connect to broker pipe: {_pipeName}");
                }

                _logger.LogDebug("Connected to broker pipe: {PipeName}", _pipeName);

                // Send request and receive response
                await WriteTokenRequestAsync(client, request, cancellationToken);
                var response = await ReadTokenResponseAsync(client, cancellationToken);

                if (response == null)
                {
                    throw new InvalidOperationException("Received null response from broker service");
                }

                _logger.LogDebug("Successfully received response from broker pipe: {PipeName}", _pipeName);
                return response;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException && cancellationToken.IsCancellationRequested))
            {
                lastException = ex;
                _logger.LogWarning(ex, "Failed to communicate with broker pipe: {PipeName} (attempt {Attempt}/{MaxAttempts})", 
                    _pipeName, attempt, _maxRetryAttempts);

                if (attempt < _maxRetryAttempts)
                {
                    var delay = TimeSpan.FromMilliseconds(Math.Min(1000 * attempt, 5000)); // Exponential backoff up to 5 seconds
                    _logger.LogDebug("Retrying in {DelayMs}ms", delay.TotalMilliseconds);
                    await Task.Delay(delay, cancellationToken);
                }
            }
        }

        var errorMessage = $"Failed to communicate with broker service after {_maxRetryAttempts} attempts";
        _logger.LogError(lastException, errorMessage);
        throw new InvalidOperationException(errorMessage, lastException);
    }

    /// <summary>
    /// Writes a token request to the named pipe stream with length prefix.
    /// </summary>
    /// <param name="stream">The named pipe stream to write to.</param>
    /// <param name="request">The token request to serialize and write.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private static async Task WriteTokenRequestAsync(NamedPipeClientStream stream, TokenRequest request, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(request, BrokerJsonContext.Default.TokenRequest);
        var messageBuffer = System.Text.Encoding.UTF8.GetBytes(json);
        var lengthBuffer = BitConverter.GetBytes(messageBuffer.Length);

        // Write length prefix
        await stream.WriteAsync(lengthBuffer, 0, lengthBuffer.Length, cancellationToken);
        
        // Write message content
        await stream.WriteAsync(messageBuffer, 0, messageBuffer.Length, cancellationToken);
        
        await stream.FlushAsync(cancellationToken);
    }

    /// <summary>
    /// Reads a token response from the named pipe stream with length prefix.
    /// </summary>
    /// <param name="stream">The named pipe stream to read from.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The deserialized token response, or null if the connection was closed.</returns>
    private static async Task<TokenResponse?> ReadTokenResponseAsync(NamedPipeClientStream stream, CancellationToken cancellationToken)
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
                    return null; // Connection closed
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
                    return null; // Connection closed
                }
                bytesRead += read;
            }

            var json = System.Text.Encoding.UTF8.GetString(messageBuffer);
            return JsonSerializer.Deserialize(json, BrokerJsonContext.Default.TokenResponse);
        }
        catch (Exception)
        {
            return null;
        }
    }
}