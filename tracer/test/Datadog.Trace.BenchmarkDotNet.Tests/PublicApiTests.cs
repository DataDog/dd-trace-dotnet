// <copyright file="PublicApiTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Tests;

namespace Datadog.Trace.BenchmarkDotNet.Tests
{
    // System.CodeDom 6.0.0 (imported by BenchmarkDotNet 0.13.2) cannot be used in these targets
#if !NETCOREAPP3_1 && !NETCOREAPP3_0 && !NETCOREAPP2_1
    public class PublicApiTests : PublicApiTestsBase
    {
        public PublicApiTests()
            : base(typeof(DatadogExtensions).Assembly)
        {
        }
    }
#endif
}
