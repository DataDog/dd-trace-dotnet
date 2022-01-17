// <copyright file="UnixDomainSocketLargePayloadTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_1_OR_GREATER

using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class UnixDomainSocketLargePayloadTests : LargePayloadTestBase
    {
        public UnixDomainSocketLargePayloadTests(ITestOutputHelper output)
            : base(output)
        {
        }

        [Fact]
        [Trait("RunOnWindows", "True")]
        public void SubmitsTraces()
        {
            EnvironmentHelper.EnableUnixDomainSockets();
            RunTest();
        }
    }
}
#endif
