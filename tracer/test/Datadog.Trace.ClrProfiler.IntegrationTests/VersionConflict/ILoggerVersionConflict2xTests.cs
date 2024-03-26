// <copyright file="ILoggerVersionConflict2xTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading.Tasks;
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
        public async Task InjectsLogs()
        {
            // One of the traces starts by manual opening a span when the background service starts,
            // and then it sends a HTTP request to the server.
            // On .NET Framework, we do not yet automatically instrument AspNetCore so instead of
            // having one distributed trace, the result is two separate traces. So expect one more trace
            // when running on .NET Framework

            // We also log inside of the web server handling, so in .NET Core expect one more log line
#if NETFRAMEWORK
            var expectedCorrelatedTraceCount = 3;
            var expectedCorrelatedSpanCount = 3;
#else
            var expectedCorrelatedTraceCount = 2;
            var expectedCorrelatedSpanCount = 4;
#endif

            using (var agent = EnvironmentHelper.GetMockAgent())
            using (await RunSampleAndWaitForExit(agent, aspNetCorePort: 0))
            {
                var spans = agent.WaitForSpans(1, 2500);
                spans.Should().HaveCountGreaterOrEqualTo(1);

                ValidateLogCorrelation(spans, _logFiles, expectedCorrelatedTraceCount, expectedCorrelatedSpanCount);
            }
        }
    }
}
