// <copyright file="OtelThreadContextTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    /// <summary>
    /// End-to-end coverage for the OTEP 4947 thread context. The record layout itself is covered by unit
    /// tests; here an independent OTEP 4719 reader verifies the real process context produced with
    /// libdatadog, while the logs verify that the native <c>otel_thread_ctx_v1</c> symbol resolves and that
    /// installing records succeeds. The tests also verify that enabling the feature does not disturb
    /// tracing. See docs/OTelContextPropagation.md.
    /// </summary>
    public class OtelThreadContextTests : TestHelper
    {
        private const string ServiceName = "otel-thread-context-test";
        private const string ServiceVersion = "1.0.0";
        private const string ServiceEnvironment = "integration-test";
        private const string SchemaVersionAttribute = "threadlocal.schema_version";
        private const string AttributeKeyMapAttribute = "threadlocal.attribute_key_map";

        public OtelThreadContextTests(ITestOutputHelper output)
            : base("Console", output)
        {
            SetServiceName(ServiceName);
            SetServiceVersion(ServiceVersion);
            SetEnvironmentVariable(ConfigurationKeys.Environment, ServiceEnvironment);
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "False")]
        public async Task PublishesThreadContextWithoutDisturbingTracing()
        {
            // OTEP 4947 relies on ELF thread-local storage, so the feature - and this test - is Linux only.
            SkipUnlessLinux();

            var logDir = SetLogDirectory();
            SetEnvironmentVariable(ConfigurationKeys.OpenTelemetry.OtelThreadContextEnabled, "1");

            using var agent = EnvironmentHelper.GetMockAgent();
            var context = await RunSampleAndReadProcessContext(agent);

            // The publisher latches itself off and logs a single warning the first time anything fails -
            // acquiring or writing the record - so the absence of that warning is what tells us the
            // native symbol resolved and every thread installed its record.
            AssertNoThreadContextFailures(logDir);

            AssertOriginalProcessContext(context);

            GetSingleAttribute(context.AdditionalAttributes, SchemaVersionAttribute)
               .Value.StringValue.Should().Be("tlsdesc_v1_dev");

            GetSingleAttribute(context.AdditionalAttributes, AttributeKeyMapAttribute)
               .Value.ArrayValue.Should().Equal("datadog.local_root_span_id");
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "False")]
        public async Task IsInertWhenDisabled()
        {
            SkipUnlessLinux();

            var logDir = SetLogDirectory();

            using var agent = EnvironmentHelper.GetMockAgent();
            var context = await RunSampleAndReadProcessContext(agent);

            AssertOriginalProcessContext(context);
            context.AdditionalAttributes.Should().NotContain(attribute => attribute.Key == SchemaVersionAttribute);
            context.AdditionalAttributes.Should().NotContain(attribute => attribute.Key == AttributeKeyMapAttribute);

            // nothing should be logged at all when the feature is off, not even the "unavailable" notice
            GetManagedLogContent(logDir).Should().NotContain("thread context");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SkipUnlessLinux() => SkipOn.AllExcept(SkipOn.PlatformValue.Linux);

        private static void AssertOriginalProcessContext(OtelProcessContextReader.OtelProcessContextSnapshot context)
        {
            GetSingleAttribute(context.ResourceAttributes, "service.name")
               .Value.StringValue.Should().Be(ServiceName);
            GetSingleAttribute(context.ResourceAttributes, "service.version")
               .Value.StringValue.Should().Be(ServiceVersion);
            GetSingleAttribute(context.ResourceAttributes, "deployment.environment.name")
               .Value.StringValue.Should().Be(ServiceEnvironment);

            GetSingleAttribute(context.AdditionalAttributes, "datadog.process_tags")
               .Value.StringValue.Should().NotBeNullOrEmpty();
        }

        private static OtelProcessContextReader.OtelProcessContextAttribute GetSingleAttribute(
            IReadOnlyList<OtelProcessContextReader.OtelProcessContextAttribute> attributes,
            string key)
        {
            var matches = attributes.Where(attribute => attribute.Key == key).ToArray();
            matches.Should().ContainSingle($"the process context should contain exactly one '{key}' attribute");
            matches[0].Value.Should().NotBeNull($"the '{key}' attribute should have a value");
            return matches[0];
        }

        private static void AssertNoThreadContextFailures(string logDir)
        {
            var log = GetManagedLogContent(logDir);

            log.Should().NotContain(
                "Unable to publish the OpenTelemetry thread context",
                "publication must succeed on every thread");

            log.Should().NotContain(
                "OpenTelemetry thread context publication was requested but is unavailable",
                "the feature is supported on this platform, so it must not fall back to a no-op");
        }

        private static string GetManagedLogContent(string logDir)
        {
            var logFiles = Directory.GetFiles(logDir, "dotnet-tracer-managed-*.log");
            logFiles.Should().NotBeEmpty("the managed tracer must have written a log");

            return string.Concat(logFiles.Select(File.ReadAllText));
        }

        private async Task<OtelProcessContextReader.OtelProcessContextSnapshot> RunSampleAndReadProcessContext(MockTracerAgent agent)
        {
            var releaseFile = Path.Combine(Path.GetTempPath(), $"otel-process-context-{Guid.NewGuid():N}");
            SetEnvironmentVariable("DD_INTERNAL_TEST_FILE_TO_WATCH", releaseFile);

            try
            {
                using var process = await StartSample(
                                        agent,
                                        arguments: "traces 1",
                                        packageVersion: string.Empty,
                                        aspNetCorePort: 5000);
                using var processHelper = new ProcessHelper(process);

                try
                {
                    var spans = await agent.WaitForSpansAsync(1);
                    spans.Should().NotBeEmpty("publishing process context must not affect tracing");

                    return await OtelProcessContextReader.ReadAsync(process.Id, TimeSpan.FromSeconds(15));
                }
                finally
                {
                    File.WriteAllText(releaseFile, string.Empty);
                    WaitForProcessResult(processHelper);
                }
            }
            finally
            {
                File.Delete(releaseFile);
            }
        }

        private string SetLogDirectory([CallerMemberName] string testName = null)
        {
            var logDir = Path.Combine(LogDirectory, testName);
            Directory.CreateDirectory(logDir);
            SetEnvironmentVariable(ConfigurationKeys.LogDirectory, logDir);
            return logDir;
        }
    }
}
