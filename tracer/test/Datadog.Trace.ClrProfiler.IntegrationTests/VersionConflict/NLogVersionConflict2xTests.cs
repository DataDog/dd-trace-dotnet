// <copyright file="NLogVersionConflict2xTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETCOREAPP2_1
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.VersionConflict
{
    public class NLogVersionConflict2xTests : LogsInjectionTestBase
    {
        private readonly LogFileTest[] _nlog40LogFileTests =
            {
                new LogFileTest()
                {
                    FileName = "log-textFile.log",
                    RegexFormat = @"{0}: {1}",
                    UnTracedLogTypes = UnTracedLogTypes.EmptyProperties,
                    PropertiesUseSerilogNaming = false
                },
                new LogFileTest()
                {
                    FileName = "log-jsonFile.log",
                    RegexFormat = @"""{0}"": {1}",
                    UnTracedLogTypes = UnTracedLogTypes.EnvServiceTracingPropertiesOnly,
                    PropertiesUseSerilogNaming = false
                }
            };

        public NLogVersionConflict2xTests(ITestOutputHelper output)
            : base(output, "LogsInjection.NLog.VersionConflict.2x")
        {
            SetServiceVersion("1.0.0");
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public void InjectsLogsWhenEnabled()
        {
            SetEnvironmentVariable("DD_LOGS_INJECTION", "true");

            var expectedCorrelatedTraceCount = 1;
            var expectedCorrelatedSpanCount = 8;

            int agentPort = TcpPortProvider.GetOpenPort();
            using (var agent = MockTracerAgent.Create(agentPort))
            using (RunSampleAndWaitForExit(agent))
            {
                var spans = agent.WaitForSpans(1, 2500);
                Assert.True(spans.Count >= 1, $"Expecting at least 1 span, only received {spans.Count}");

                ValidateLogCorrelation(spans, _nlog40LogFileTests, expectedCorrelatedTraceCount, expectedCorrelatedSpanCount);
            }
        }
    }
}
#endif
