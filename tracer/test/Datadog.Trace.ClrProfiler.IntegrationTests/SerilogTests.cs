// <copyright file="SerilogTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class SerilogTests : LogsInjectionTestBase
    {
        private readonly LogFileTest _txtFile =
            new LogFileTest()
            {
                FileName = "log-textFile.log",
                RegexFormat = @"{0}: {1}",
                UnTracedLogTypes = UnTracedLogTypes.EmptyProperties,
                PropertiesUseSerilogNaming = true
            };

        public SerilogTests(ITestOutputHelper output)
            : base(output, "LogsInjection.Serilog")
        {
            SetServiceVersion("1.0.0");
            EnableDebugMode();
        }

        [SkippableTheory]
        [MemberData(nameof(PackageVersions.Serilog), MemberType = typeof(PackageVersions))]
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

                var logFiles = GetLogFiles(packageVersion, logsInjectionEnabled: true);
                ValidateLogCorrelation(spans, logFiles, expectedTraceCount: 1, packageVersion);
            }
        }

        [SkippableTheory]
        [MemberData(nameof(PackageVersions.Serilog), MemberType = typeof(PackageVersions))]
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

                var logFiles = GetLogFiles(packageVersion, logsInjectionEnabled: false);
                ValidateLogCorrelation(spans, logFiles, expectedTraceCount: 0, packageVersion, disableLogCorrelation: true);
            }
        }

        private LogFileTest[] GetLogFiles(string packageVersion, bool logsInjectionEnabled)
        {
            var isPost200 =
#if NETCOREAPP
                // enabled in default version for .NET Core
                string.IsNullOrWhiteSpace(packageVersion) || new Version(packageVersion) >= new Version("2.0.0");
#else
                !string.IsNullOrWhiteSpace(packageVersion) && new Version(packageVersion) >= new Version("2.0.0");
#endif
            if (!isPost200)
            {
                // no json file, always the same format
                return new[] { _txtFile };
            }

            var unTracedLogFormat = logsInjectionEnabled
                                        ? UnTracedLogTypes.EnvServiceTracingPropertiesOnly
                                        : UnTracedLogTypes.None;

            var jsonFile =  new LogFileTest()
            {
                FileName = "log-jsonFile.log",
                RegexFormat = @"""{0}"":{1}",
                UnTracedLogTypes = unTracedLogFormat,
                PropertiesUseSerilogNaming = true
            };

            return new[] { _txtFile, jsonFile };
        }
    }
}
