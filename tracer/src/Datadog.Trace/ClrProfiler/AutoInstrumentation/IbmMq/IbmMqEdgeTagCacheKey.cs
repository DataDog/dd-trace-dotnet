// <copyright file="IbmMqEdgeTagCacheKey.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.IbmMq;

/// <summary>
/// Value-type cache key for IBM MQ edge tags. Using a named struct avoids boxing and
/// is compatible with all supported target frameworks.
/// <see cref="IsConsume"/> distinguishes produce (direction:out) from consume (direction:in)
/// so that both directions share a single cache type without key collision.
/// </summary>
internal readonly record struct IbmMqEdgeTagCacheKey(string QueueName, bool IsConsume);
