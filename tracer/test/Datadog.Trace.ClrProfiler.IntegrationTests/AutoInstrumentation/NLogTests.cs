// <copyright file="NLogTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog.DirectSubmission.Formatting;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging.DirectSubmission;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Util;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class NLogTests : LogsInjectionTestBase
    {
        private const string CustomContextKey = "CustomContextKey";
        private const string CustomContextValue = "CustomContextValue";
        private const string HostName = "integration_nlog_tests";
        private const string JsonFileWithInjection = "log-jsonFile-withInject.log";
        private const string JsonFileNoInjection = "log-jsonFile-noInject.log";

        // The sample logs every message once per supported context, with the context appended as "(context: [name])"
        private static readonly Regex LoggingContextRegex = new(@"\(context: (\w+)\)");

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
            SetEnvironmentVariable("DD_TRACE_128_BIT_TRACEID_GENERATION_ENABLED", "true");
            SetEnvironmentVariable("DD_TRACE_128_BIT_TRACEID_LOGGING_ENABLED", "true");
            SetEnvironmentVariable("DD_LOGS_INJECTION", "true");
        }

        private enum ConfigurationType
        {
            /// <summary>
            /// No configuration provided at all.
            /// </summary>
            None,

            /// <summary>
            /// Configuration file contains targets that are and aren't pre-configured with logs injection related elements.
            /// (e.g., "includeMdc = true" is present in one JSON target, and omitted from the other)
            /// </summary>
            Both
        }

        /// <summary>
        /// The contexts the sample uses when logging, which depend on the version of NLog
        /// </summary>
        private enum LoggingContext
        {
            /// <summary>
            /// No logging context.
            /// </summary>
            None,

            /// <summary>
            /// Use MDC (including the legacy NLog 1.x API) as logging context.
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

        // Each version uses different hooks/targets, so cover both ID formats with shipping on and off per version.
        [SkippableTheory]
        [CombinatorialData]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        public Task InjectsLogsWhenEnabled(
            [PackageVersionData(nameof(PackageVersions.NLog))] string packageVersion,
            bool enableLogShipping,
            bool enable128BitInjection)
            => RunLogsInjectionTest(packageVersion, enableLogShipping, enable128BitInjection, logsInjectionEnabled: true);

        [SkippableTheory]
        [CombinatorialData]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        public Task DoesNotInjectLogsWhenDisabled(
            [PackageVersionData(nameof(PackageVersions.NLog))] string packageVersion,
            bool enableLogShipping)
            => RunLogsInjectionTest(packageVersion, enableLogShipping, enable128BitInjection: true, logsInjectionEnabled: false);

        [SkippableTheory]
        [CombinatorialData]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        public async Task DirectlyShipsLogsWithoutConfiguration(
            [PackageVersionData(nameof(PackageVersions.NLog), minInclusive: "2.1.0")] string packageVersion,
            bool logsInjectionEnabled)
        {
            // Shipping logs with a configuration file is covered by InjectsLogsWhenEnabled,
            // this covers the case where there's no NLog configuration at all, so we create one
            SetInstrumentationVerification();
            SetEnvironmentVariable("DD_LOGS_INJECTION", logsInjectionEnabled ? "true" : "false");
            using var logsIntake = new MockLogsIntake();
            using var telemetry = ConfigureLogShipping(logsIntake, enabled: true);

            using var agent = EnvironmentHelper.GetMockAgent();
            using var processResult = await RunSampleAndWaitForExit(agent, packageVersion: packageVersion, arguments: nameof(ConfigurationType.None));

            var spans = await agent.WaitForSpansAsync(1, 2500, operationName: "transaction");
            spans.Should().ContainSingle();

            AssertShippedLogs(logsIntake.Logs, GetExpectedContexts(packageVersion), logsInjectionEnabled ? spans : null, use128Bits: true);
            VerifyInstrumentation(processResult.Process);
            await telemetry.AssertIntegrationEnabledAsync(IntegrationId.NLog);
        }

        private static Version GetNLogVersion(string packageVersion)
        {
            if (packageVersion is null or "")
            {
                // The default versions used by the sample, see ApiVersion in LogsInjection.NLog.csproj
                return EnvironmentHelper.IsCoreClr() ? new Version("6.0.0") : new Version("2.1.0");
            }

            return new Version(packageVersion);
        }

        private static LoggingContext[] GetExpectedContexts(string packageVersion)
        {
            var version = GetNLogVersion(packageVersion);
            if (version >= new Version("5.0.0"))
            {
                return [LoggingContext.None, LoggingContext.Mdc, LoggingContext.Mdlc, LoggingContext.ScopeContext];
            }

            if (version >= new Version("4.1.0"))
            {
                return [LoggingContext.None, LoggingContext.Mdc, LoggingContext.Mdlc];
            }

            return [LoggingContext.None, LoggingContext.Mdc];
        }

        private static LoggingContext GetLoggingContext(string message)
        {
            var match = LoggingContextRegex.Match(message);
            if (!match.Success)
            {
                throw new Exception($"Log message did not include the logging context: {message}");
            }

            return (LoggingContext)Enum.Parse(typeof(LoggingContext), match.Groups[1].Value);
        }

        private static LogFileTest GetJsonTestFile(string fileName, UnTracedLogTypes unTracedLogType) => new()
        {
            FileName = fileName,
            RegexFormat = @"""{0}"":\s*{1}",
            UnTracedLogTypes = unTracedLogType,
            PropertiesUseSerilogNaming = false
        };

        private static void AssertLoggingContexts(IReadOnlyCollection<string> logs, LoggingContext[] expectedContexts)
        {
            using var scope = new AssertionScope();
            logs.Should().HaveCount(3 * expectedContexts.Length);
            logs.Where(x => x.Contains(ExcludeMessagePrefix + "Entering Datadog scope."))
                .Select(GetLoggingContext).Should().BeEquivalentTo(expectedContexts, "each context should log before the span");
            logs.Where(x => !x.Contains(ExcludeMessagePrefix))
                .Select(GetLoggingContext).Should().BeEquivalentTo(expectedContexts, "each context should log inside the span");
            logs.Where(x => x.Contains(ExcludeMessagePrefix + "Exited Datadog scope."))
                .Select(GetLoggingContext).Should().BeEquivalentTo(expectedContexts, "each context should log after the span");
        }

        private async Task RunLogsInjectionTest(string packageVersion, bool enableLogShipping, bool enable128BitInjection, bool logsInjectionEnabled)
        {
            SetEnvironmentVariable("DD_LOGS_INJECTION", logsInjectionEnabled ? "true" : "false");
            SetEnvironmentVariable("DD_TRACE_128_BIT_TRACEID_LOGGING_ENABLED", enable128BitInjection ? "true" : "false");
            SetInstrumentationVerification();
            using var logsIntake = new MockLogsIntake();
            using var telemetry = ConfigureLogShipping(logsIntake, enableLogShipping);
            using var agent = EnvironmentHelper.GetMockAgent();
            using var processResult = await RunSampleAndWaitForExit(agent, packageVersion: packageVersion, arguments: nameof(ConfigurationType.Both));

            var spans = await agent.WaitForSpansAsync(1, 2500, operationName: "transaction");
            spans.Should().ContainSingle();

            if (!EnvironmentHelper.IsCoreClr() && !enableLogShipping)
            {
                processResult.StandardOutput.Should().Contain("Invoked PluginApplication.Program.Invoke()");
            }

            var expectedContexts = GetExpectedContexts(packageVersion);
            var testFiles = GetTestFiles(packageVersion, logsInjectionEnabled);
            var correlatedCount = logsInjectionEnabled ? 1 : 0;
            ValidateLogCorrelation(spans, testFiles, correlatedCount, correlatedCount, packageVersion, disableLogCorrelation: !logsInjectionEnabled, use128Bits: enable128BitInjection);
            VerifyLogFiles(testFiles, packageVersion, expectedContexts, logsInjectionEnabled);
            VerifyInstrumentation(processResult.Process);

            // Log injection supports NLog 1.x/2.0, but the configuration hook that adds
            // the direct-submission target starts at NLog 2.1.
            if (enableLogShipping && GetNLogVersion(packageVersion) >= new Version("2.1.0"))
            {
                AssertShippedLogs(logsIntake.Logs, expectedContexts, logsInjectionEnabled ? spans : null, enable128BitInjection);
                await telemetry.AssertIntegrationEnabledAsync(IntegrationId.NLog);
            }
            else
            {
                logsIntake.Logs.Should().BeEmpty();
            }
        }

        private MockTelemetryAgent ConfigureLogShipping(MockLogsIntake logsIntake, bool enabled)
        {
            // Calls across AppDomains (on .NET Framework) result in duplicate telemetry, which breaks the telemetry assertions
            SetEnvironmentVariable("INCLUDE_CROSS_DOMAIN_CALL", enabled ? "false" : "true");
            // Configure the intake even when disabled so unexpected log submissions fail the test.
            EnableDirectLogSubmission(logsIntake.Port, enabled ? nameof(IntegrationId.NLog) : string.Empty, HostName);
            return enabled ? this.ConfigureTelemetry() : null;
        }

        private void AssertShippedLogs(IReadOnlyCollection<MockLogsIntake.Log> logs, LoggingContext[] expectedContexts, IReadOnlyCollection<MockSpan> correlatedSpans, bool use128Bits)
        {
            using var scope = new AssertionScope();
            AssertLoggingContexts(logs.Select(x => x.Message).ToArray(), expectedContexts);
            logs.Should()
                .OnlyContain(x => x.Service == "LogsInjection.NLog")
                .And.OnlyContain(x => x.Env == "integration_tests")
                .And.OnlyContain(x => x.Version == "1.0.0")
                .And.OnlyContain(x => x.Host == HostName)
                .And.OnlyContain(x => x.Source == "csharp")
                .And.OnlyContain(x => x.Exception == null)
                .And.OnlyContain(x => x.LogLevel == DirectSubmissionLogLevel.Information)
                .And.OnlyContain(x => x.TryGetProperty(NLogLogFormatter.LoggerNameKey).Exists);

            var tracedLogs = logs.Where(x => !x.Message.Contains(ExcludeMessagePrefix)).ToList();

            foreach (var log in logs)
            {
                var property = log.TryGetProperty(CustomContextKey);
                if (GetLoggingContext(log.Message) == LoggingContext.None)
                {
                    property.Exists.Should().BeFalse($"'{log.Message}' should not have the custom context property");
                }
                else
                {
                    property.Exists.Should().BeTrue($"'{log.Message}' should have the custom context property");
                    property.Value.Should().Be(CustomContextValue);
                }
            }

            if (correlatedSpans is null)
            {
                logs.Should()
                    .OnlyContain(x => StringUtil.IsNullOrEmpty(x.TraceId))
                    .And.OnlyContain(x => StringUtil.IsNullOrEmpty(x.SpanId));
                return;
            }

            var traceIds = GetExpectedLogTraceIds(correlatedSpans, use128Bits);
            var spanIds = correlatedSpans.Select(x => x.SpanId.ToString()).ToList();

            tracedLogs.Should()
                      .OnlyContain(x => traceIds.Contains(x.TraceId))
                      .And.OnlyContain(x => spanIds.Contains(x.SpanId));
            tracedLogs.Select(x => x.SpanId).Distinct().Should().ContainSingle();
            logs.Where(x => x.Message.Contains(ExcludeMessagePrefix))
                .Should()
                .OnlyContain(x => StringUtil.IsNullOrEmpty(x.TraceId))
                .And.OnlyContain(x => StringUtil.IsNullOrEmpty(x.SpanId));
        }

        private void VerifyLogFiles(LogFileTest[] testFiles, string packageVersion, LoggingContext[] expectedContexts, bool logsInjectionEnabled)
        {
            var version = GetNLogVersion(packageVersion);
            foreach (var testFile in testFiles)
            {
                var logFilePath = Path.Combine(EnvironmentHelper.GetSampleApplicationOutputDirectory(packageVersion), testFile.FileName);
                var logs = GetLogFileContents(logFilePath);

                using var scope = new AssertionScope(testFile.FileName);
                AssertLoggingContexts(logs, expectedContexts);

                if (testFile.FileName == _textFileWithInjection.FileName)
                {
                    // Text layouts always render these fields. Require empty values after the span
                    // (and when injection is disabled), rather than only checking that the keys exist.
                    string[] emptyProperties = logsInjectionEnabled
                                                   ? ["trace_id", "span_id"]
                                                   : ["env", "service", "version", "trace_id", "span_id"];
                    foreach (var log in logs.Where(x => !logsInjectionEnabled || x.Contains(ExcludeMessagePrefix)))
                    {
                        foreach (var property in emptyProperties)
                        {
                            log.Should().MatchRegex(string.Format(testFile.RegexFormat, @"dd\." + property, "\"\""));
                        }
                    }

                    continue;
                }

                // Before 4.4.10, auto-configuration adds only the five Datadog attributes.
                // Our preconfigured layouts use includeMdc/includeMdlc starting with NLog.46.config.
                var includesContextProperties = (logsInjectionEnabled && version >= new Version("4.4.10"))
                                             || (testFile.FileName == JsonFileWithInjection && version >= new Version("4.6.0"));
                var contextPropertyRegex = string.Format(testFile.RegexFormat, CustomContextKey, $@"""{CustomContextValue}""");
                foreach (var log in logs)
                {
                    if (!logsInjectionEnabled)
                    {
                        log.Should().NotMatchRegex(@"""dd\.(env|service|version|trace_id|span_id)""\s*:");
                    }

                    if (includesContextProperties && GetLoggingContext(log) != LoggingContext.None)
                    {
                        log.Should().MatchRegex(contextPropertyRegex);
                    }
                    else
                    {
                        log.Should().NotContain(CustomContextKey);
                    }
                }
            }
        }

        private LogFileTest[] GetTestFiles(string packageVersion, bool logsInjectionEnabled)
        {
            if (GetNLogVersion(packageVersion) < new Version("4.0.0"))
            {
                // pre 4.0 can't write to json file
                return [_textFileWithInjection];
            }

            var unTracedLogType = logsInjectionEnabled switch
            {
                // When logs injection is enabled, untraced logs get env, service etc
                true => UnTracedLogTypes.EnvServiceTracingPropertiesOnly,
                // When logs injection is disabled, no enrichment
                false => UnTracedLogTypes.None
            };

            return [_textFileWithInjection, GetJsonTestFile(JsonFileWithInjection, unTracedLogType), GetJsonTestFile(JsonFileNoInjection, unTracedLogType)];
        }
    }
}
