// <copyright file="EdgeTagCache.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;

namespace Datadog.Trace.DataStreamsMonitoring;

/// <summary>
/// Process-wide cache of edge tag arrays, keyed by a caller-supplied value type.
/// One dictionary instance exists per distinct TKey type (static generic class pattern).
/// This avoids boxing and lets each integration use its own natural key shape.
/// </summary>
internal static class EdgeTagCache<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    internal static readonly ConcurrentDictionary<TKey, string[]> Cache = new();
}
