// <copyright file="HighConcurrencyTestCollection.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Xunit;

namespace Datadog.Trace.Tests
{
    /// <summary>
    /// Tests that deliberately either run many operations concurrently to assert on precise interleaving
    /// (e.g. maximum-overlap counts) or are sensitive to timing issues. These tests are prone to false failures
    /// from resource starvation when they run in parallel with other tests.
    /// </summary>
    [CollectionDefinition(nameof(HighConcurrencyTestCollection), DisableParallelization = true)]
    public class HighConcurrencyTestCollection
    {
    }
}
