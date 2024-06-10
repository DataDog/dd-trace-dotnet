// <copyright file="TransportTestsCollection.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if NETCOREAPP3_1_OR_GREATER
using Xunit;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.CI;

[CollectionDefinition(nameof(TransportTestsCollection), DisableParallelization = true)]
public class TransportTestsCollection
{
}

#endif
