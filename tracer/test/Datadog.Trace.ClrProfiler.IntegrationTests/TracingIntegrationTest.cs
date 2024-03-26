// <copyright file="TracingIntegrationTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public abstract class TracingIntegrationTest : TestHelper
    {
        public TracingIntegrationTest(string sampleAppName, ITestOutputHelper output)
            : base(sampleAppName, output)
        {
        }

        public TracingIntegrationTest(string sampleAppName, string samplePathOverrides, ITestOutputHelper output)
            : base(sampleAppName, samplePathOverrides, output)
        {
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
    }
}
