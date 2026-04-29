// <copyright file="ConsumeEdgeTagCacheKey.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka;

/// <summary>
/// Value-type cache key for consume edge tags. Using a named struct avoids boxing and
/// is compatible with all supported target frameworks.
/// </summary>
internal readonly record struct ConsumeEdgeTagCacheKey(string GroupId, string Topic, string ClusterId);
