// <copyright file="DefaultTransportLargePayloadTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    [Collection("LargePayloadTests")]
    public class DefaultTransportLargePayloadTests : LargePayloadTestBase
    {
        public DefaultTransportLargePayloadTests(ITestOutputHelper output)
            : base(output)
        {
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        public Task SubmitsTraces() => RunTest();
    }
}
