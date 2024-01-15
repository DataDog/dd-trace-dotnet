// <copyright file="TracingIntegrationTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public abstract class TracingIntegrationTest : TestHelper
    {
        private ITestOutputHelper _output;

        public TracingIntegrationTest(string sampleAppName, ITestOutputHelper output)
            : base(sampleAppName, output)
        {
            _output = output;
        }

        public TracingIntegrationTest(string sampleAppName, string samplePathOverrides, ITestOutputHelper output)
            : base(sampleAppName, samplePathOverrides, output)
        {
            _output = output;
        }

        public abstract Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion);

        public void ValidateIntegrationSpans(IEnumerable<MockSpan> spans, string metadataSchemaVersion, string expectedServiceName, bool isExternalSpan)
        {
            foreach (var span in spans)
            {
                var result = ValidateIntegrationSpan(span, metadataSchemaVersion);
                Assert.True(result.Success, result.ToString());

                Assert.Equal(expectedServiceName, span.Service);
                if (isExternalSpan == true)
                {
                    Assert.False(span.Tags?.ContainsKey(Tags.Version), "External service span should not have service version tag.");
                }
            }
        }

        internal async Task RunTestAndAssertTelemetry(Func<Task<MockTelemetryAgent>> actualTest, IntegrationId integrationToAssert)
        {
            // The server implementation of named pipes is flaky so have 3 attempts
            var attemptsRemaining = 3;
            while (true)
            {
                attemptsRemaining--;
                var telemetry = await actualTest();
                try
                {
                    // We know what the issue is - there's a shutdown bug, which fails during the final flush
                    // We've grabbed memory dumps from it and we don't know how to fix it.
                    // So let's retry if it fails
                    telemetry.AssertIntegrationEnabled(integrationToAssert);
                    return;
                }
                catch (Exception ex) when (ex.Message.Contains("IsRequestType(\"app-closing\"), but no such item was found."))
                {
                    await ReportRetry(_output, attemptsRemaining, ex);
                }
            }
        }
    }
}
