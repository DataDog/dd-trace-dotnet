// <copyright file="TracingIntegrationTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.TestHelpers;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public abstract class TracingIntegrationTest : TestHelper
    {
        public TracingIntegrationTest(string sampleAppName, ITestOutputHelper output)
            : base(sampleAppName, output)
        {
        }

        public abstract Result ValidateIntegrationSpan(MockSpan span);
    }
}
