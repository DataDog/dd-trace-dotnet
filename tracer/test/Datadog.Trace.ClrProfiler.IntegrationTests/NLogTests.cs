// <copyright file="NLogTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog.DirectSubmission.Formatting;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging.DirectSubmission;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;
using Xunit.Abstractions;

#pragma warning disable 0618 // MDC and MDLC are obsolete

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class NLogTests : LogsInjectionTestBase
    {
        private const string ContextNone = "None";
        private const string CustomContextKey = "CustomContextKey";
        private const string CustomContextValue = "CustomContextValue";

        private readonly LogFileTest _textFile = new()
        {
            FileName = "log-textFile.log",
            RegexFormat = @"{0}: {1}",
            // txt format can't conditionally add properties
            UnTracedLogTypes = UnTracedLogTypes.EmptyProperties,
            PropertiesUseSerilogNaming = false
        };

        private readonly LogFileTest _textFile2 = new()
        {
            FileName = "log-textFile2.log",
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

        public static IEnumerable<object[]> GetTestData()
        {
            var minScopeContext = new Version("5.0.0");
            var minMdlc = new Version("4.6.0");
            foreach (var item in PackageVersions.NLog)
            {
                Version version;
                var defaultSamples = (string)item[0] == string.Empty;
                if (defaultSamples)
                {
                    // LogsInjection.NLog uses different versions depending on framework
                    version = EnvironmentHelper.IsCoreClr() ?
                                  new Version("5.0.0") :
                                  new Version("2.1.0");
                }
                else
                {
                    version = new Version((string)item[0]);
                }

                yield return item.Concat(false).Concat(ContextNone);
                yield return item.Concat(true).Concat(ContextNone);

                if (version >= minScopeContext)
                {
                    yield return item.Concat(false).Concat("ScopeContext");
                    yield return item.Concat(true).Concat("ScopeContext");
                }

                if (version >= minMdlc)
                {
                    yield return item.Concat(false).Concat("Mdlc");
                    yield return item.Concat(true).Concat("Mdlc");
                }
            }
        }

        [SkippableTheory]
        [MemberData(nameof(GetTestData))]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        public void InjectsLogsWhenEnabled(string packageVersion, bool enableLogShipping, string context)
        {
            SetEnvironmentVariable("DD_LOGS_INJECTION", "true");
            SetInstrumentationVerification();
            using var logsIntake = new MockLogsIntake();
            if (enableLogShipping)
            {
                EnableDirectLogSubmission(logsIntake.Port, nameof(IntegrationId.NLog), nameof(InjectsLogsWhenEnabled));
            }

            var expectedCorrelatedTraceCount = 1;
            var expectedCorrelatedSpanCount = 1;

            using (var agent = EnvironmentHelper.GetMockAgent())
            using (var processResult = RunSampleAndWaitForExit(agent, packageVersion: packageVersion, arguments: context))
            {
                var spans = agent.WaitForSpans(1, 2500);
                Assert.True(spans.Count >= 1, $"Expecting at least 1 span, only received {spans.Count}");

                var testFiles = GetTestFiles(packageVersion);
                ValidateLogCorrelation(spans, testFiles, expectedCorrelatedTraceCount, expectedCorrelatedSpanCount, packageVersion);
                VerifyInstrumentation(processResult.Process);
                VerifyContextProperties(testFiles, packageVersion, context);
            }
        }

        [SkippableTheory]
        [MemberData(nameof(GetTestData))]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        public void DoesNotInjectLogsWhenDisabled(string packageVersion, bool enableLogShipping, string context)
        {
            SetEnvironmentVariable("DD_LOGS_INJECTION", "false");
            SetInstrumentationVerification();
            using var logsIntake = new MockLogsIntake();
            if (enableLogShipping)
            {
                EnableDirectLogSubmission(logsIntake.Port, nameof(IntegrationId.NLog), nameof(InjectsLogsWhenEnabled));
            }

            var expectedCorrelatedTraceCount = 0;
            var expectedCorrelatedSpanCount = 0;

            using (var agent = EnvironmentHelper.GetMockAgent())
            using (var processResult = RunSampleAndWaitForExit(agent, packageVersion: packageVersion, arguments: context))
            {
                var spans = agent.WaitForSpans(1, 2500);
                Assert.True(spans.Count >= 1, $"Expecting at least 1 span, only received {spans.Count}");

                var testFiles = GetTestFiles(packageVersion, logsInjectionEnabled: false);
                ValidateLogCorrelation(spans, testFiles, expectedCorrelatedTraceCount, expectedCorrelatedSpanCount, packageVersion, disableLogCorrelation: true);

                VerifyInstrumentation(processResult.Process);
                VerifyContextProperties(testFiles, packageVersion, context);
            }
        }

        [SkippableTheory]
        [MemberData(nameof(GetTestData))]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        public void DirectlyShipsLogs(string packageVersion, bool enableLogShipping, string context)
        {
            if (!enableLogShipping) { throw new Xunit.SkipException("Direct log submission disabled does not apply to this test"); }

            var hostName = "integration_nlog_tests";
            using var logsIntake = new MockLogsIntake();

            SetInstrumentationVerification();
            SetEnvironmentVariable("DD_LOGS_INJECTION", "true");
            SetEnvironmentVariable("INCLUDE_CROSS_DOMAIN_CALL", "false");
            EnableDirectLogSubmission(logsIntake.Port, nameof(IntegrationId.NLog), hostName);

            using var telemetry = this.ConfigureTelemetry();
            using var agent = EnvironmentHelper.GetMockAgent();
            using var processResult = RunSampleAndWaitForExit(agent, packageVersion: packageVersion, arguments: context);

            ExitCodeException.ThrowIfNonZero(processResult.ExitCode, processResult.StandardError);

            var logs = logsIntake.Logs;

            using var scope = new AssertionScope();
            logs.Should().NotBeNull();
            logs.Should().HaveCountGreaterOrEqualTo(3);
            logs.Should()
                .OnlyContain(x => x.Service == "LogsInjection.NLog")
                .And.OnlyContain(x => x.Env == "integration_tests")
                .And.OnlyContain(x => x.Version == "1.0.0")
                .And.OnlyContain(x => x.Host == hostName)
                .And.OnlyContain(x => x.Source == "csharp")
                .And.OnlyContain(x => x.Exception == null)
                .And.OnlyContain(x => x.LogLevel == DirectSubmissionLogLevel.Information)
                .And.OnlyContain(x => x.TryGetProperty(NLogLogFormatter.LoggerNameKey).Exists);

            logs
               .Where(x => !x.Message.Contains(ExcludeMessagePrefix))
               .Should()
               .HaveCount(1)
               .And.OnlyContain(x => !string.IsNullOrEmpty(x.TraceId))
               .And.OnlyContain(x => !string.IsNullOrEmpty(x.SpanId));
            VerifyInstrumentation(processResult.Process);

            if (context != ContextNone)
            {
                Func<MockLogsIntake.Log, string, string, bool> hasProperty = (log, key, value) =>
                {
                    var prop = log.TryGetProperty(key);
                    return prop.Exists && prop.Value == value;
                };
                logs.Should().Contain(x => hasProperty(x, CustomContextKey, CustomContextValue));
            }

            telemetry.AssertIntegrationEnabled(IntegrationId.NLog);
        }

        private void VerifyContextProperties(LogFileTest[] testFiles, string packageVersion, string context)
        {
            if (context == ContextNone) { return; }

            // Skip for versions that don't support json
            if (testFiles.Length < 4) { return; }

            var test = testFiles[1];
            var logFilePath = Path.Combine(EnvironmentHelper.GetSampleApplicationOutputDirectory(packageVersion), test.FileName);
            var logs = GetLogFileContents(logFilePath);
            foreach (var log in logs)
            {
                log.Should().MatchRegex(string.Format(test.RegexFormat, CustomContextKey, $@"""{CustomContextValue}"""));
            }
        }

        private LogFileTest[] GetTestFiles(string packageVersion, bool logsInjectionEnabled = true)
        {
            if (packageVersion is null or "")
            {
#if NETFRAMEWORK
                packageVersion = "2.1.0";
#else
                packageVersion = "4.5.0";
#endif
            }

            var version = new Version(packageVersion);
            if (version < new Version("4.0.0"))
            {
                // pre 4.0 can't write to json file
                return new[] { _textFile, _textFile2 };
            }

            var unTracedLogType = logsInjectionEnabled switch
            {
                // When logs injection is enabled, untraced logs get env, service etc
                true => UnTracedLogTypes.EnvServiceTracingPropertiesOnly,
                // When logs injection is enabled, no enrichment
                false => UnTracedLogTypes.None,
            };

            return new[] { _textFile, _textFile2, GetJsonTestFile(unTracedLogType) };
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
