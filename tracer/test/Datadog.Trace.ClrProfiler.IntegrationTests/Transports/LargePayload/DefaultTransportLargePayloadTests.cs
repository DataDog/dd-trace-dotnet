// <copyright file="DefaultTransportLargePayloadTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_1

using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    [Collection(nameof(DefaultTransportLargePayloadTests))]
    public class DefaultTransportLargePayloadTests : LargePayloadTestBase
    {
        public DefaultTransportLargePayloadTests(ITestOutputHelper output)
            : base(output)
        {
        }

        [Fact]
        public void SubmitsTraces()
        {
            RunTest();
        }
    }
}
#endif
