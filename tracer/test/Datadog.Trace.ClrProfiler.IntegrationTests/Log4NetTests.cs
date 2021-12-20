// <copyright file="Log4NetTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class Log4NetTests : LogsInjectionTestBase
    {
        private readonly LogFileTest[] _nlogPre205LogFileTests =
            {
                new LogFileTest()
                {
                    FileName = "log-textFile.log",
                    RegexFormat = @"{0}: {1}",
                    UnTracedLogTypes = UnTracedLogTypes.EmptyProperties,
                    PropertiesUseSerilogNaming = false
                }
            };

        private readonly LogFileTest[] _nlog205LogFileTests =
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
                    RegexFormat = @"""{0}"":{1}",
                    UnTracedLogTypes = UnTracedLogTypes.EmptyProperties,
                    PropertiesUseSerilogNaming = false
                }
            };

        public Log4NetTests(ITestOutputHelper output)
            : base(output, "LogsInjection.Log4Net")
        {
            SetServiceVersion("1.0.0");
        }

        [SkippableTheory]
        [MemberData(nameof(PackageVersions.log4net), MemberType = typeof(PackageVersions))]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public void InjectsLogsWhenEnabled(string packageVersion)
        {
            SetEnvironmentVariable("DD_LOGS_INJECTION", "true");

            var expectedCorrelatedTraceCount = 1;
            var expectedCorrelatedSpanCount = 1;

            using (var agent = EnvironmentHelper.GetMockAgent())
            using (RunSampleAndWaitForExit(agent.Port, packageVersion: packageVersion))
            {
                var spans = agent.WaitForSpans(1, 2500);
                Assert.True(spans.Count >= 1, $"Expecting at least 1 span, only received {spans.Count}");

#if NETFRAMEWORK
                if (!string.IsNullOrWhiteSpace(packageVersion) && new Version(packageVersion) >= new Version("2.0.5"))
                {
                    ValidateLogCorrelation(spans, _nlog205LogFileTests, expectedCorrelatedTraceCount, expectedCorrelatedSpanCount, packageVersion);
                }
                else
                {
                    ValidateLogCorrelation(spans, _nlogPre205LogFileTests, expectedCorrelatedTraceCount, expectedCorrelatedSpanCount, packageVersion);
                }
#else
                // Regardless of package version, for .NET Core just assert against raw log lines
                ValidateLogCorrelation(spans, _nlogPre205LogFileTests, expectedCorrelatedTraceCount, expectedCorrelatedSpanCount, packageVersion);
#endif
            }
        }

        [SkippableTheory]
        [MemberData(nameof(PackageVersions.log4net), MemberType = typeof(PackageVersions))]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public void DoesNotInjectLogsWhenDisabled(string packageVersion)
        {
            SetEnvironmentVariable("DD_LOGS_INJECTION", "false");

            var expectedCorrelatedTraceCount = 0;
            var expectedCorrelatedSpanCount = 0;

            using (var agent = EnvironmentHelper.GetMockAgent())
            using (RunSampleAndWaitForExit(agent.Port, packageVersion: packageVersion))
            {
                var spans = agent.WaitForSpans(1, 2500);
                Assert.True(spans.Count >= 1, $"Expecting at least 1 span, only received {spans.Count}");

#if NETFRAMEWORK
                if (!string.IsNullOrWhiteSpace(packageVersion) && new Version(packageVersion) >= new Version("2.0.5"))
                {
                    ValidateLogCorrelation(spans, _nlog205LogFileTests, expectedCorrelatedTraceCount, expectedCorrelatedSpanCount, packageVersion, disableLogCorrelation: true);
                }
                else
                {
                    ValidateLogCorrelation(spans, _nlogPre205LogFileTests, expectedCorrelatedTraceCount, expectedCorrelatedSpanCount, packageVersion, disableLogCorrelation: true);
                }
#else
                // Regardless of package version, for .NET Core just assert against raw log lines
                ValidateLogCorrelation(spans, _nlogPre205LogFileTests, expectedCorrelatedTraceCount, expectedCorrelatedSpanCount, packageVersion, disableLogCorrelation: true);
#endif
            }
        }
    }
}
