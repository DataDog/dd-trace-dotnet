// <copyright file="DevArtDataDBCommandSmokeTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.SmokeTests
{
    public class DevArtDataDBCommandSmokeTest : SmokeTestBase
    {
        public DevArtDataDBCommandSmokeTest(ITestOutputHelper output)
            : base(output, "Devart.Data.DBCommand")
        {
        }

        [SkippableFact]
        [Trait("Category", "Smoke")]
        public async Task HasSpans()
        {
            await CheckForSmoke();
            Assert.True(Spans.Count > 0);
        }
    }
}
