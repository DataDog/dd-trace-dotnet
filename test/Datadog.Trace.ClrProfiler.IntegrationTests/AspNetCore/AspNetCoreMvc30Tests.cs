// <copyright file="AspNetCoreMvc30Tests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_0
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AspNetCore
{
    public class AspNetCoreMvc30Tests : AspNetCoreMvcTestBase
    {
        public AspNetCoreMvc30Tests(ITestOutputHelper output)
            : base("AspNetCoreMvc30", output, serviceVersion: "1.0.0")
        {
            // EnableDebugMode();
        }

        [Fact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public async Task MeetsAllAspNetCoreMvcExpectations()
        {
            // No package versions are relevant because this is built-in
            var spans = await RunTraceTestOnSelfHosted(string.Empty);

            // output log file
            var sampleProjectDirectory = EnvironmentHelper.GetSampleApplicationOutputDirectory();
            var logFile = Path.Combine(sampleProjectDirectory, "log", "Karambolo", "log.txt");
            File.Exists(logFile).Should().BeTrue($"'{logFile}' should exist");
            var logJson = await File.ReadAllTextAsync(logFile);

            logJson.Should().NotBeNullOrEmpty();
            var traceIds = spans
                          .Select(x => x.TraceId.ToString())
                          .Distinct();
            logJson.Should().ContainAll(traceIds);
        }
    }
}
#endif
