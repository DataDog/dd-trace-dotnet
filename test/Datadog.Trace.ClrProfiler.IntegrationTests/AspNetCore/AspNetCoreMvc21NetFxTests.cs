// <copyright file="AspNetCoreMvc21NetFxTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET461
using System.Collections.Generic;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AspNetCore
{
    public class AspNetCoreMvc21NetFxTests : AspNetCoreMvcTestBase
    {
        public AspNetCoreMvc21NetFxTests(ITestOutputHelper output)
            : base("AspNetCoreMvc21", output, serviceVersion: "1.0.0")
        {
        }

        [Fact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public async Task MeetsAllAspNetCoreMvcExpectations()
        {
            // No package versions are relevant because this is built-in
            // As tracing is not enabled on .NET Framework, we won't get any spans
            // We _would_ get logs injection, but trace_id would always be set to 0,
            // so we don't do any logs injection to avoid confusion
            var emptySpans = new List<MockTracerAgent.Span>();
            await RunTraceTestOnSelfHosted(string.Empty, (_, _) => emptySpans);

            // We won't have traceId injected into the logs, but service/env will be
            RunLogInjectionTest(emptySpans);
        }
    }
}
#endif
