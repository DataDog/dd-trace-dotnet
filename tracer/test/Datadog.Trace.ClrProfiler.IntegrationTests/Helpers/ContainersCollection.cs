// <copyright file="ContainersCollection.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#pragma warning disable SA1649 // File name should match first type name (this will just store all the classes)
#pragma warning disable SA1402 // File may only contain a single type (this will just store all the classes)
using Datadog.Trace.TestHelpers.AutoInstrumentation.Containers;
using Xunit;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.Helpers
{
    // Container-backed collections must set DisableParallelization = true. CustomTestFramework only auto-serializes
    // collections whose display name contains the assembly namespace, which named [CollectionDefinition]s lose.
    [CollectionDefinition(Name, DisableParallelization = true)]
    public class AerospikeCollection : ICollectionFixture<AerospikeFixture>
    {
        public const string Name = "Aerospike";
    }
}

#pragma warning restore SA1649 // File name should match first type name
#pragma warning restore SA1402 // File may only contain a single type
