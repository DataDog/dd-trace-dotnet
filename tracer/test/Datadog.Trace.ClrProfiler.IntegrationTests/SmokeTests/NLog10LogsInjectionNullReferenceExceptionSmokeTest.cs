// <copyright file="NLog10LogsInjectionNullReferenceExceptionSmokeTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.SmokeTests
{
    public class NLog10LogsInjectionNullReferenceExceptionSmokeTest : SmokeTestBase
    {
        public NLog10LogsInjectionNullReferenceExceptionSmokeTest(ITestOutputHelper output)
            : base(output, "NLog10LogsInjection.NullReferenceException", maxTestRunSeconds: 90)
        {
            SetEnvironmentVariable("DD_LOGS_INJECTION", "true");
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
