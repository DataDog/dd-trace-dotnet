// <copyright file="Log4NetSerializationExceptionSmokeTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.SmokeTests
{
    public class Log4NetSerializationExceptionSmokeTest : SmokeTestBase
    {
        public Log4NetSerializationExceptionSmokeTest(ITestOutputHelper output)
            : base(output, "Log4Net.SerializationException", maxTestRunSeconds: 120)
        {
        }

        [Fact]
        [Trait("Category", "Smoke")]
        public void NoExceptions()
        {
            CheckForSmoke(shouldDeserializeTraces: false);
        }
    }
}
#endif
