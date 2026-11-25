// <copyright file="AzureStorageQueuesEdgeTagCacheKey.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.StorageQueues;

/// <summary>
/// Value-type cache key for Azure Storage Queues edge tags, shared by produce and consume.
/// <see cref="IsConsume"/> distinguishes produce (direction:out) from consume (direction:in).
/// </summary>
internal readonly record struct AzureStorageQueuesEdgeTagCacheKey(string QueueName, bool IsConsume);
