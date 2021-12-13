// <copyright file="ILoggerVersionConflict2xTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.VersionConflict
{
    // ReSharper disable once InconsistentNaming
    public class ILoggerVersionConflict2xTests : LogsInjectionTestBase
    {
        private readonly LogFileTest[] _logFiles =
        {
            new LogFileTest
            {
                FileName = "simple.log",
                RegexFormat = @"""{0}"":{1}",
                UnTracedLogTypes = UnTracedLogTypes.EnvServiceTracingPropertiesOnly,
                PropertiesUseSerilogNaming = true
            },
        };

        public ILoggerVersionConflict2xTests(ITestOutputHelper output)
            : base(output, "LogsInjection.ILogger.VersionConflict.2x")
        {
            SetServiceVersion("1.0.0");
            SetEnvironmentVariable("DD_LOGS_INJECTION", "true");
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public void InjectsLogs()
        {
#if NETFRAMEWORK
            var expectedTraceCount = 3;
#else
            var expectedTraceCount = 2;
#endif
            using (var agent = EnvironmentHelper.GetMockAgent())
            using (RunSampleAndWaitForExit(agent.Port, aspNetCorePort: 0))
            {
                var spans = agent.WaitForSpans(1, 2500);
                spans.Should().HaveCountGreaterOrEqualTo(1);

                ValidateLogCorrelation(spans, _logFiles, expectedTraceCount);
            }
        }
    }
}
