// <copyright file="BaggageTestCollection.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Xunit;

namespace Datadog.Trace.Tests
{
    /// <summary>
    /// Serializes the tests that make a <see cref="Baggage"/> instance current. The ambient baggage
    /// is static state, so these tests cannot run alongside each other or alongside anything else
    /// that reads it. Pair membership of this collection with <c>[BaggageRestorer]</c>.
    /// </summary>
    [CollectionDefinition(nameof(BaggageTestCollection), DisableParallelization = true)]
    public class BaggageTestCollection
    {
    }
}
