// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using System.Runtime.Versioning;
using System.Text;
using Azure.Core;
using Azure.Mcp.Core.Areas.Server.Commands.Discovery;
using Azure.Mcp.Core.Areas.Server.Commands.Runtime;
using Azure.Mcp.Core.Areas.Server.Commands.ToolLoading;
using Azure.Mcp.Core.Areas.Server.Options;
using Azure.Mcp.Core.Helpers;
using Azure.Mcp.Core.Services.Azure.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;

namespace Azure.Mcp.Core.Areas.Server.Commands;

// This is intentionally placed after the namespace declaration to avoid
// conflicts with Azure.Mcp.Core.Areas.Server.Options
using Options = Microsoft.Extensions.Options.Options;

/// <summary>
/// Extension methods for configuring Azure MCP server services.
/// </summary>
public static class AzureMcpServiceCollectionExtensions
{
    private const string DefaultServerName = "Azure MCP Server";

    /// <summary>
    /// Adds the Azure MCP server services to the specified <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="serviceStartOptions">The options for configuring the server.</param>
    /// <returns>The service collection with MCP server services added.</returns>
    public static IServiceCollection AddAzureMcpServer(this IServiceCollection services, ServiceStartOptions serviceStartOptions)
    {
        // Register HTTP client services
        services.AddHttpClientServices();

        // Register options for service start
        services.AddSingleton(serviceStartOptions);
        services.AddSingleton(Options.Create(serviceStartOptions));

        // Register default tool loader options from service start options
        var defaultToolLoaderOptions = new ToolLoaderOptions
        {
            Namespace = serviceStartOptions.Namespace,
            ReadOnly = serviceStartOptions.ReadOnly ?? false,
        };

        if (serviceStartOptions.Mode == ModeTypes.NamespaceProxy)
        {
            if (defaultToolLoaderOptions.Namespace == null || defaultToolLoaderOptions.Namespace.Length == 0)
            {
                defaultToolLoaderOptions = defaultToolLoaderOptions with { Namespace = ["extension"] };
            }
        }

        services.AddSingleton(defaultToolLoaderOptions);
        services.AddSingleton(Options.Create(defaultToolLoaderOptions));

        // Register tool loader strategies
        services.AddSingleton<CommandFactoryToolLoader>();
        services.AddSingleton(sp =>
        {
            return new RegistryToolLoader(
                sp.GetRequiredService<RegistryDiscoveryStrategy>(),
                sp.GetRequiredService<IOptions<ToolLoaderOptions>>(),
                sp.GetRequiredService<ILogger<RegistryToolLoader>>()
            );
        });

        services.AddSingleton<SingleProxyToolLoader>();
        services.AddSingleton<CompositeToolLoader>();
        services.AddSingleton<ServerToolLoader>();

        // Register server discovery strategies
        services.AddSingleton<CommandGroupDiscoveryStrategy>();
        services.AddSingleton<CompositeDiscoveryStrategy>();
        services.AddSingleton<RegistryDiscoveryStrategy>();

        // Register server providers
        services.AddSingleton<CommandGroupServerProvider>();
        services.AddSingleton<RegistryServerProvider>();

        // Register MCP runtimes
        services.AddSingleton<IMcpRuntime, McpRuntime>();
        
        // Register appropriate request context factory based on OBO configuration
        if (serviceStartOptions.IsOboParent())
        {
            services.AddSingleton<IAzMcpRequestContextFactory, OboParentRequestContextFactory>();
            
            // Register On-Behalf-Of authentication services for HTTP transport
            services.AddHttpContextAccessor();
            services.AddScoped<IAuthenticationContext, HttpAuthenticationContext>();
            
            // Register OBO Token Credential Factory for parent processes
            services.AddSingleton<IOboTokenCredentialFactory, OboTokenCredentialFactory>();
            
            // Register broker service infrastructure for inter-process token communication
            // Named pipes are supported on Windows, Linux, and macOS
            if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                services.AddSingleton<IOboParentBrokerService, OboParentBrokerService>();
                services.AddSingleton<NamedPipeServerService>();
                services.AddHostedService<BrokerHostedService>();
            }
        }
        else if (serviceStartOptions.IsOboChild())
        {
            services.AddSingleton<IAzMcpRequestContextFactory, OboChildRequestContextFactory>();
            
            // Register OBO Proxy Token Credential Factory for child processes
            // Named pipes are supported on Windows, Linux, and macOS
            if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                services.AddSingleton<IOboTokenCredentialFactory, OboProxyTokenCredentialFactory>();
            }
        }
        else
        {
            services.AddSingleton<IAzMcpRequestContextFactory, DefaultRequestContextFactory>();
        }
        // When OBO is disabled, IAuthenticationContext is not registered.
        // BaseAzureService.GetCredential() will use GetService<IAuthenticationContext>() which returns null,
        // causing it to fall back to DefaultAzureCredential for all scenarios.

        // Register MCP discovery strategies based on proxy mode
        if (serviceStartOptions.Mode == ModeTypes.SingleToolProxy || serviceStartOptions.Mode == ModeTypes.NamespaceProxy)
        {
            services.AddSingleton<IMcpDiscoveryStrategy>(sp =>
            {
                var discoveryStrategies = new List<IMcpDiscoveryStrategy>
                {
                    sp.GetRequiredService<RegistryDiscoveryStrategy>(),
                    sp.GetRequiredService<CommandGroupDiscoveryStrategy>(),
                };

                var logger = sp.GetRequiredService<ILogger<CompositeDiscoveryStrategy>>();
                return new CompositeDiscoveryStrategy(discoveryStrategies, logger);
            });
        }

        // Configure tool loading based on mode
        if (serviceStartOptions.Mode == ModeTypes.SingleToolProxy)
        {
            services.AddSingleton<IToolLoader, SingleProxyToolLoader>();
        }
        else if (serviceStartOptions.Mode == ModeTypes.NamespaceProxy)
        {
            services.AddSingleton<IToolLoader>(sp =>
            {
                var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
                var toolLoaders = new List<IToolLoader>
                {
                    sp.GetRequiredService<ServerToolLoader>(),
                };

                // Append extension commands when no other namespaces are specified.
                if (defaultToolLoaderOptions.Namespace?.SequenceEqual(["extension"]) == true)
                {
                    toolLoaders.Add(sp.GetRequiredService<CommandFactoryToolLoader>());
                }

                return new CompositeToolLoader(toolLoaders, loggerFactory.CreateLogger<CompositeToolLoader>());
            });
        }
        else if (serviceStartOptions.Mode == ModeTypes.All)
        {
            services.AddSingleton<IMcpDiscoveryStrategy, RegistryDiscoveryStrategy>();
            services.AddSingleton<IToolLoader>(sp =>
            {
                var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
                var toolLoaders = new List<IToolLoader>
                {
                    sp.GetRequiredService<RegistryToolLoader>(),
                    sp.GetRequiredService<CommandFactoryToolLoader>(),
                };

                return new CompositeToolLoader(toolLoaders, loggerFactory.CreateLogger<CompositeToolLoader>());
            });
        }

        var mcpServerOptions = services
            .AddOptions<McpServerOptions>()
            .Configure<IMcpRuntime>((mcpServerOptions, mcpRuntime) =>
            {
                var mcpServerOptionsBuilder = services.AddOptions<McpServerOptions>();
                var entryAssembly = Assembly.GetEntryAssembly();
                var assemblyName = entryAssembly?.GetName();
                var serverName = entryAssembly?.GetCustomAttribute<AssemblyTitleAttribute>()?.Title ?? DefaultServerName;

                mcpServerOptions.ProtocolVersion = "2024-11-05";
                mcpServerOptions.ServerInfo = new Implementation
                {
                    Name = serverName,
                    Version = assemblyName?.Version?.ToString() ?? "1.0.0-beta"
                };

                mcpServerOptions.Capabilities = new ServerCapabilities
                {
                    Tools = new ToolsCapability()
                    {
                        CallToolHandler = mcpRuntime.CallToolHandler,
                        ListToolsHandler = mcpRuntime.ListToolsHandler,
                    }
                };

                // Add instructions for the server
                mcpServerOptions.ServerInstructions = GetServerInstructions();
            });

        var mcpServerBuilder = services.AddMcpServer();

        if (serviceStartOptions.EnableInsecureTransports)
        {
            mcpServerBuilder.WithHttpTransport();
        }
        else
        {
            mcpServerBuilder.WithStdioServerTransport();
        }

        return services;
    }

    /// <summary>
    /// Generates comprehensive instructions for using the Azure MCP Server effectively.
    /// Includes Azure best practices from embedded resource files.
    /// </summary>
    /// <returns>Instructions text for LLM interactions with the Azure MCP Server.</returns>
    private static string GetServerInstructions()
    {
        var instructions = new StringBuilder();

        try
        {
            var azureRulesContent = LoadAzureRulesForBestPractices();
            if (!string.IsNullOrEmpty(azureRulesContent))
            {
                instructions.AppendLine(azureRulesContent);
            }
        }
        catch (Exception)
        {
            // Fallback if resources are not available
            instructions.AppendLine("**Note**: Azure rules resources are not available in this configuration.");
            instructions.AppendLine("An error occurred while loading Azure rules.");
        }

        return instructions.ToString();
    }

    /// <summary>
    /// Loads Azure rules for calling bestpractices tool from embedded resource files.
    /// </summary>
    /// <returns>Combined content from all Azure best practices resource files.</returns>
    private static string LoadAzureRulesForBestPractices()
    {
        var coreAssembly = typeof(AzureMcpServiceCollectionExtensions).Assembly;
        var azureRulesContent = new StringBuilder();

        // List of known best practices resource files
        var resourceFile = "azure-rules.txt";

        try
        {
            string resourceName = EmbeddedResourceHelper.FindEmbeddedResource(coreAssembly, resourceFile);
            string content = EmbeddedResourceHelper.ReadEmbeddedResource(coreAssembly, resourceName);

            azureRulesContent.AppendLine(content);
            azureRulesContent.AppendLine();
        }
        catch (Exception)
        {
            // Log the error but continue processing other files
            azureRulesContent.AppendLine($"### Error loading {resourceFile}");
            azureRulesContent.AppendLine("An error occurred while loading this section.");
            azureRulesContent.AppendLine();
        }

        return azureRulesContent.ToString();
    }
}
