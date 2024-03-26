// <copyright file="PublicApiTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.IO;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Tests;
using Xunit.Abstractions;

namespace Datadog.Trace.BenchmarkDotNet.Tests
{
    public class PublicApiTests : PublicApiTestsBase
    {
        public PublicApiTests(ITestOutputHelper output)
            : base(typeof(DatadogExtensions).Assembly, output)
        {
        }
    }
}
