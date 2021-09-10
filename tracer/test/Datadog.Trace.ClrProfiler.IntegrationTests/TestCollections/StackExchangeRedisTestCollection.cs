// <copyright file="StackExchangeRedisTestCollection.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Xunit;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.TestCollections
{
    [CollectionDefinition(nameof(StackExchangeRedisTestCollection), DisableParallelization = true)]
    public class StackExchangeRedisTestCollection
    {
    }
}
