// <copyright file="DataStreamsTransactionCacheTestCollection.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Xunit;

namespace Datadog.Trace.Tests.DataStreamsMonitoring;

// Tests that assert on specific checkpoint IDs in the static cache must not run in parallel with other
// tests that create DataStreamsTransactionInfo objects — they share a static ConcurrentDictionary + counter.
[CollectionDefinition(nameof(DataStreamsTransactionCacheTestCollection), DisableParallelization = true)]
public class DataStreamsTransactionCacheTestCollection
{
}
