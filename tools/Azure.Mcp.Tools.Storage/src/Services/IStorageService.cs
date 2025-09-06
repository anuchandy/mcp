// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Mcp.Core.Areas.Server.Commands.Runtime;
using Azure.Mcp.Core.Options;
using Azure.Mcp.Tools.Storage.Models;
using Azure.Storage.Blobs.Models;

namespace Azure.Mcp.Tools.Storage.Services;

public interface IStorageService
{
    Task<List<StorageAccountInfo>> GetStorageAccounts(
        McpUserContext userContext,
        string subscription,
        string? tenant = null,
        RetryPolicyOptions? retryPolicy = null);

    Task<StorageAccountInfo> GetStorageAccountDetails(
        McpUserContext userContext,
        string account,
        string subscription,
        string? tenant = null,
        RetryPolicyOptions? retryPolicy = null);

    Task<StorageAccountInfo> CreateStorageAccount(
        McpUserContext userContext,
        string account,
        string resourceGroup,
        string location,
        string subscription,
        string? sku = null,
        string? accessTier = null,
        bool? enableHierarchicalNamespace = null,
        string? tenant = null,
        RetryPolicyOptions? retryPolicy = null);

    Task<List<string>> ListContainers(
        McpUserContext userContext,
        string account,
        string subscription,
        string? tenant = null,
        RetryPolicyOptions? retryPolicy = null);

    Task<List<string>> ListTables(
        McpUserContext userContext,
        string account,
        string subscription,
        AuthMethod authMethod = AuthMethod.Credential,
        string? connectionString = null,
        string? tenant = null,
        RetryPolicyOptions? retryPolicy = null);

    Task<List<string>> ListBlobs(
        McpUserContext userContext,
        string accountName,
        string container,
        string subscription,
        string? tenant = null,
        RetryPolicyOptions? retryPolicy = null);

    Task<BlobProperties> GetBlobDetails(
        McpUserContext userContext,
        string account,
        string container,
        string blob,
        string subscription,
        string? tenant = null,
        RetryPolicyOptions? retryPolicy = null);

    Task<BlobContainerProperties> GetContainerDetails(
        McpUserContext userContext,
        string account,
        string container,
        string subscription,
        string? tenant = null,
        RetryPolicyOptions? retryPolicy = null);

    Task<BlobContainerProperties> CreateContainer(
        McpUserContext userContext,
        string account,
        string container,
        string subscription,
        string? tenant = null,
        RetryPolicyOptions? retryPolicy = null);

    Task<List<DataLakePathInfo>> ListDataLakePaths(
        McpUserContext userContext,
        string account,
        string fileSystem,
        bool recursive,
        string subscription,
        string? filterPath = null,
        string? tenant = null,
        RetryPolicyOptions? retryPolicy = null);

    Task<DataLakePathInfo> CreateDirectory(
        McpUserContext userContext,
        string account,
        string directoryPath,
        string subscription,
        string? tenant = null,
        RetryPolicyOptions? retryPolicy = null);

    Task<(List<string> SuccessfulBlobs, List<string> FailedBlobs)> SetBlobTierBatch(
        McpUserContext userContext,
        string account,
        string container,
        string tier,
        string[] blobs,
        string subscription,
        string? tenant = null,
        RetryPolicyOptions? retryPolicy = null);

    Task<List<FileShareItemInfo>> ListFilesAndDirectories(
        McpUserContext userContext,
        string account,
        string share,
        string directoryPath,
        string? prefix,
        string subscription,
        string? tenant = null,
        RetryPolicyOptions? retryPolicy = null);

    Task<QueueMessageSendResult> SendQueueMessage(
        McpUserContext userContext,
        string account,
        string queue,
        string message,
        int? timeToLiveInSeconds,
        int? visibilityTimeoutInSeconds,
        string subscription,
        string? tenant = null,
        RetryPolicyOptions? retryPolicy = null);

    Task<BlobUploadResult> UploadBlob(
        McpUserContext userContext,
        string account,
        string container,
        string blob,
        string localFilePath,
        string subscription,
        string? tenant = null,
        RetryPolicyOptions? retryPolicy = null);
}
