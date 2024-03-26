// <copyright file="NLogTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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

        private readonly LogFileTest _textFileWithInjection = new()
        {
            FileName = "log-textFile-withInject.log",
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

        public enum ConfigurationType
        {
            /// <summary>
            /// No configuration provided at all.
            /// </summary>
            None,

            /// <summary>
            /// All targets in configuration will _not_ contain targets pre-configured with logs injection related elements.
            /// (e.g., "includeMdc = true" would be omitted from the JSON target)
            /// </summary>
            NoLogsInjection,

            /// <summary>
            /// All targets in configuration _will_ contain targets pre-configured with logs injection related elements.
            /// (e.g., "includeMdc = true" would be present in the JSON target)
            /// </summary>
            LogsInjection,

            /// <summary>
            /// Configuration file contains targets that are and aren't pre-configured with logs injection related elements.
            /// </summary>
            Both
        }

        public enum DirectLogSubmission
        {
            /// <summary>
            /// DirectLogSubmission is enabled.
            /// </summary>
            Enable,

            /// <summary>
            /// DirectLogSubmission is disabled.
            /// </summary>
            Disable
        }

        public enum LoggingContext
        {
            /// <summary>
            /// No logging context.
            /// </summary>
            None,

            /// <summary>
            /// Use MDC as logging context.
            /// </summary>
            Mdc,

            /// <summary>
            /// Use MDLC as logging context.
            /// </summary>
            Mdlc,

            /// <summary>
            /// Use ScopeContext as logging context.
            /// </summary>
            ScopeContext
        }

        public static IEnumerable<object[]> GetTestDataDirectSubmission()
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

                foreach (var agentless in Enum.GetValues(typeof(DirectLogSubmission)))
                {
                    if ((DirectLogSubmission)agentless == DirectLogSubmission.Disable)
                    {
                        continue;
                    }

                    foreach (var configType in Enum.GetValues(typeof(ConfigurationType)))
                    {
                        if ((ConfigurationType)configType == ConfigurationType.NoLogsInjection && version < new Version("4.0.0"))
                        {
                            continue; // pre 4.0.0 doesn't have JSON support
                        }

                        yield return item.Concat(agentless).Concat(LoggingContext.None).Concat(configType);

                        if (version >= minScopeContext)
                        {
                            yield return item.Concat(agentless).Concat(LoggingContext.ScopeContext).Concat(configType);
                        }

                        if (version >= minMdlc)
                        {
                            yield return item.Concat(agentless).Concat(LoggingContext.Mdlc).Concat(configType);
                        }
                    }
                }
            }
        }

        public static IEnumerable<object[]> GetTestDataLogsInjection()
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

                foreach (var agentless in Enum.GetValues(typeof(DirectLogSubmission)))
                {
                    foreach (var configType in Enum.GetValues(typeof(ConfigurationType)))
                    {
                        if ((ConfigurationType)configType == ConfigurationType.None)
                        {
                            // if we don't have a config there won't be any targets to inject logs to
                            continue;
                        }

                        if ((ConfigurationType)configType == ConfigurationType.NoLogsInjection && version < new Version("4.0.0"))
                        {
                            continue; // pre 4.0.0 doesn't have JSON support
                        }

                        yield return item.Concat(agentless).Concat(LoggingContext.None).Concat(configType);

                        if (version >= minScopeContext)
                        {
                            yield return item.Concat(agentless).Concat(LoggingContext.ScopeContext).Concat(configType);
                        }

                        if (version >= minMdlc)
                        {
                            yield return item.Concat(agentless).Concat(LoggingContext.Mdlc).Concat(configType);
                        }
                    }
                }
            }
        }

        public static IEnumerable<object[]> DoesNotInjectLogsWhenDisabledTestData()
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

                foreach (var agentless in Enum.GetValues(typeof(DirectLogSubmission)))
                {
                    yield return item.Concat(agentless).Concat(LoggingContext.None).Concat(ConfigurationType.LogsInjection);

                    if (version >= minScopeContext)
                    {
                        yield return item.Concat(agentless).Concat(LoggingContext.ScopeContext).Concat(ConfigurationType.LogsInjection);
                    }

                    if (version >= minMdlc)
                    {
                        yield return item.Concat(agentless).Concat(LoggingContext.Mdlc).Concat(ConfigurationType.LogsInjection);
                    }
                }
            }
        }

        [SkippableTheory]
        [MemberData(nameof(GetTestDataLogsInjection))]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        public async Task InjectsLogsWhenEnabled(string packageVersion, DirectLogSubmission enableLogShipping, string context, ConfigurationType configType)
        {
            SetEnvironmentVariable("DD_LOGS_INJECTION", "true");
            SetInstrumentationVerification();
            using var logsIntake = new MockLogsIntake();
            if (enableLogShipping == DirectLogSubmission.Enable)
            {
                EnableDirectLogSubmission(logsIntake.Port, nameof(IntegrationId.NLog), nameof(InjectsLogsWhenEnabled));
            }

            var expectedCorrelatedTraceCount = 1;
            var expectedCorrelatedSpanCount = 1;

            using (var agent = EnvironmentHelper.GetMockAgent())
            using (var processResult = await RunSampleAndWaitForExit(agent, packageVersion: packageVersion, arguments: string.Join(" ", new object[] { context, configType })))
            {
                var spans = agent.WaitForSpans(1, 2500);
                Assert.True(spans.Count >= 1, $"Expecting at least 1 span, only received {spans.Count}");

                var testFiles = GetTestFiles(packageVersion, true, configType);
                ValidateLogCorrelation(spans, testFiles, expectedCorrelatedTraceCount, expectedCorrelatedSpanCount, packageVersion);
                VerifyInstrumentation(processResult.Process);
                VerifyContextProperties(testFiles, packageVersion, context);
            }
        }

        [SkippableTheory]
        [MemberData(nameof(DoesNotInjectLogsWhenDisabledTestData))]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        public async Task DoesNotInjectLogsWhenDisabled(string packageVersion, DirectLogSubmission enableLogShipping, string context, ConfigurationType configType)
        {
            if (configType != ConfigurationType.LogsInjection)
            {
                throw new Xunit.SkipException("Does not inject logs when disabled without any log configuration targets doesn't apply to this test.");
            }

            SetEnvironmentVariable("DD_LOGS_INJECTION", "false");
            SetInstrumentationVerification();
            using var logsIntake = new MockLogsIntake();
            if (enableLogShipping == DirectLogSubmission.Enable)
            {
                EnableDirectLogSubmission(logsIntake.Port, nameof(IntegrationId.NLog), nameof(InjectsLogsWhenEnabled));
            }

            var expectedCorrelatedTraceCount = 0;
            var expectedCorrelatedSpanCount = 0;

            using (var agent = EnvironmentHelper.GetMockAgent())
            using (var processResult = await RunSampleAndWaitForExit(agent, packageVersion: packageVersion, arguments: string.Join(" ", new object[] { context, configType })))
            {
                var spans = agent.WaitForSpans(1, 2500);
                Assert.True(spans.Count >= 1, $"Expecting at least 1 span, only received {spans.Count}");

                var testFiles = GetTestFiles(packageVersion, logsInjectionEnabled: false, configType);
                ValidateLogCorrelation(spans, testFiles, expectedCorrelatedTraceCount, expectedCorrelatedSpanCount, packageVersion, disableLogCorrelation: true);

                VerifyInstrumentation(processResult.Process);
                VerifyContextProperties(testFiles, packageVersion, context);
            }
        }

        [SkippableTheory]
        [MemberData(nameof(GetTestDataDirectSubmission))]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        public async Task DirectlyShipsLogs(string packageVersion, DirectLogSubmission enableLogShipping, string context, ConfigurationType configType)
        {
            if (enableLogShipping != DirectLogSubmission.Enable) { throw new Xunit.SkipException("Direct log submission disabled does not apply to this test"); }

            var hostName = "integration_nlog_tests";
            using var logsIntake = new MockLogsIntake();

            SetInstrumentationVerification();
            SetEnvironmentVariable("DD_LOGS_INJECTION", "true");
            SetEnvironmentVariable("INCLUDE_CROSS_DOMAIN_CALL", "false");
            EnableDirectLogSubmission(logsIntake.Port, nameof(IntegrationId.NLog), hostName);

            using var telemetry = this.ConfigureTelemetry();
            using var agent = EnvironmentHelper.GetMockAgent();
            using var processResult = await RunSampleAndWaitForExit(agent, packageVersion: packageVersion, arguments: string.Join(" ", new object[] { context, configType }));

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
            foreach (var testFile in testFiles)
            {
                if (testFile.FileName.Contains("json"))
                {
                    var test = testFile; // jsonFile
                    var logFilePath = Path.Combine(EnvironmentHelper.GetSampleApplicationOutputDirectory(packageVersion), test.FileName);
                    var logs = GetLogFileContents(logFilePath);
                    foreach (var log in logs)
                    {
                        log.Should().MatchRegex(string.Format(test.RegexFormat, CustomContextKey, $@"""{CustomContextValue}"""));
                    }
                }
            }
        }

        private LogFileTest[] GetTestFiles(string packageVersion, bool logsInjectionEnabled = true, ConfigurationType configType = ConfigurationType.Both)
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
                if (configType == ConfigurationType.Both || configType == ConfigurationType.LogsInjection)
                {
                    return new[] { _textFileWithInjection };
                }
                else if (configType == ConfigurationType.NoLogsInjection)
                {
                    throw new Exception("NLog versions below 4.0.0 don't have JSON, so no automated logs injection");
                }
            }

            var unTracedLogType = logsInjectionEnabled switch
            {
                // When logs injection is enabled, untraced logs get env, service etc
                true => UnTracedLogTypes.EnvServiceTracingPropertiesOnly,
                // When logs injection is disabled, no enrichment
                false => UnTracedLogTypes.None
            };

            if (logsInjectionEnabled && configType == ConfigurationType.Both)
            {
                return new[] { _textFileWithInjection, GetJsonTestFile(unTracedLogType), GetJsonTestFileNoInjection(unTracedLogType) };
            }
            else if (logsInjectionEnabled && configType == ConfigurationType.LogsInjection)
            {
                return new[] { _textFileWithInjection, GetJsonTestFile(unTracedLogType) };
            }
            else if (logsInjectionEnabled && configType == ConfigurationType.NoLogsInjection)
            {
                return new[] {  GetJsonTestFileNoInjection(unTracedLogType) };
            }
            else
            {
                return new[] { _textFileWithInjection, GetJsonTestFile(unTracedLogType) };
            }
        }

        private LogFileTest GetJsonTestFile(UnTracedLogTypes unTracedLogType) => new()
        {
            FileName = "log-jsonFile-withInject.log",
            RegexFormat = @"""{0}"": {1}",
            UnTracedLogTypes = unTracedLogType,
            PropertiesUseSerilogNaming = false
        };

        private LogFileTest GetJsonTestFileNoInjection(UnTracedLogTypes unTracedLogType) => new()
        {
            FileName = "log-jsonFile-noInject.log",
            RegexFormat = @"""{0}"": {1}",
            UnTracedLogTypes = unTracedLogType,
            PropertiesUseSerilogNaming = false
        };
    }
}
