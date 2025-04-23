// <copyright file="AdoNetIntegrationTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.ClrProfiler.IntegrationTests.Helpers;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AdoNet
{
    [IntegrationArea(KnownTestAreas.AdoNet)]
    public abstract class AdoNetIntegrationTest : TracingIntegrationTest
    {
        protected AdoNetIntegrationTest(string sampleAppName, ITestOutputHelper output)
            : base(sampleAppName, output)
        {
        }

        protected AdoNetIntegrationTest(string sampleAppName, string samplePathOverrides, ITestOutputHelper output)
            : base(sampleAppName, samplePathOverrides, output)
        {
        }
    }
}
