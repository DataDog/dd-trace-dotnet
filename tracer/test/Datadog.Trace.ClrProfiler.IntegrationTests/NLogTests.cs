// <copyright file="NLogTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class NLogTests : LogsInjectionTestBase
    {
        private readonly LogFileTest _textFile = new()
        {
            FileName = "log-textFile.log",
            RegexFormat = @"{0}: {1}",
            // txt format can't conditionally add properties
            UnTracedLogTypes = UnTracedLogTypes.EmptyProperties,
            PropertiesUseSerilogNaming = false
        };

        public NLogTests(ITestOutputHelper output)
            : base(output, "LogsInjection.NLog")
        {
            SetServiceVersion("1.0.0");
        }

        [SkippableTheory]
        [MemberData(nameof(PackageVersions.NLog), MemberType = typeof(PackageVersions))]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public void InjectsLogsWhenEnabled(string packageVersion)
        {
            SetEnvironmentVariable("DD_LOGS_INJECTION", "true");

            using (var agent = EnvironmentHelper.GetMockAgent())
            using (RunSampleAndWaitForExit(agent.Port, packageVersion: packageVersion))
            {
                var spans = agent.WaitForSpans(1, 2500);
                Assert.True(spans.Count >= 1, $"Expecting at least 1 span, only received {spans.Count}");

                var testFiles = GetTestFiles(packageVersion);
                ValidateLogCorrelation(spans, testFiles, expectedTraceCount: 1, packageVersion);
            }
        }

        [SkippableTheory]
        [MemberData(nameof(PackageVersions.NLog), MemberType = typeof(PackageVersions))]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public void DoesNotInjectLogsWhenDisabled(string packageVersion)
        {
            SetEnvironmentVariable("DD_LOGS_INJECTION", "false");

            using (var agent = EnvironmentHelper.GetMockAgent())
            using (RunSampleAndWaitForExit(agent.Port, packageVersion: packageVersion))
            {
                var spans = agent.WaitForSpans(1, 2500);
                Assert.True(spans.Count >= 1, $"Expecting at least 1 span, only received {spans.Count}");

                var testFiles = GetTestFiles(packageVersion, logsInjectionEnabled: false);
                ValidateLogCorrelation(spans, testFiles, expectedTraceCount: 0, packageVersion, disableLogCorrelation: true);
            }
        }

        private LogFileTest[] GetTestFiles(string packageVersion, bool logsInjectionEnabled = true)
        {
            if (packageVersion is null or "")
            {
#if NETFRAMEWORK
                packageVersion = "4.1.2";
#else
                packageVersion = "4.5.0";
#endif
            }

            var version = new Version(packageVersion);
            if (version < new Version("4.0.0"))
            {
                // pre 4.0 can't write to json file
                return new[] { _textFile };
            }

            var unTracedLogType = logsInjectionEnabled switch
            {
                // When logs injection is enabled, untraced logs get env, service etc
                true => UnTracedLogTypes.EnvServiceTracingPropertiesOnly,
                // When logs injection is enabled, no enrichment
                false => UnTracedLogTypes.None,
            };

            return new[] { _textFile, GetJsonTestFile(unTracedLogType) };
        }

        private LogFileTest GetJsonTestFile(UnTracedLogTypes unTracedLogType) => new()
        {
            FileName = "log-jsonFile.log",
            RegexFormat = @"""{0}"": {1}",
            UnTracedLogTypes = unTracedLogType,
            PropertiesUseSerilogNaming = false
        };
    }
}
