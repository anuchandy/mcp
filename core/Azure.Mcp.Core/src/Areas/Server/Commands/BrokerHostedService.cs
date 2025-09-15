// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Runtime.Versioning;
using Azure.Mcp.Core.Services.Azure.Authentication;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Azure.Mcp.Core.Areas.Server.Commands;

/// <summary>
/// Hosted service for running the token broker service using named pipes.
/// This service manages the lifecycle of the named pipe server that allows OBO Child processes
/// to request tokens from the OBO Parent process.
/// </summary>
[SupportedOSPlatform("windows")]
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
public sealed class BrokerHostedService : BackgroundService
{
    private readonly NamedPipeServerService _pipeServer;
    private readonly ILogger<BrokerHostedService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="BrokerHostedService"/> class.
    /// </summary>
    /// <param name="pipeServer">The named pipe server service.</param>
    /// <param name="logger">The logger for diagnostic information.</param>
    public BrokerHostedService(NamedPipeServerService pipeServer, ILogger<BrokerHostedService> logger)
    {
        _pipeServer = pipeServer ?? throw new ArgumentNullException(nameof(pipeServer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting token broker service with named pipe server");
        
        try
        {
            await _pipeServer.RunAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Token broker service stopped gracefully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token broker service encountered an error");
            throw;
        }
        finally
        {
            _logger.LogInformation("Token broker service has stopped");
        }
    }

    /// <inheritdoc />
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping token broker service");
        await base.StopAsync(cancellationToken);
    }
}