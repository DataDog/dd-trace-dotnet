// <copyright file="OtelThreadContextTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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
    /// End-to-end coverage for the OTEP 4947 thread context. Independent test readers verify both the
    /// real process context produced with libdatadog and an active thread's record reached through its
    /// exported <c>otel_thread_ctx_v1</c> TLS slot. The tests also verify that enabling the feature does
    /// not disturb tracing. See docs/OTelContextPropagation.md.
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
            var contexts = await RunSampleAndReadContexts(agent);

            // The publisher latches itself off and logs a single warning the first time anything fails -
            // acquiring or writing the record - so make sure no other publication attempt failed.
            AssertNoThreadContextFailures(logDir);

            AssertOriginalProcessContext(contexts.ProcessContext);

            GetSingleAttribute(contexts.ProcessContext.AdditionalAttributes, SchemaVersionAttribute)
               .Value.StringValue.Should().Be("tlsdesc_v1_dev");

            GetSingleAttribute(contexts.ProcessContext.AdditionalAttributes, AttributeKeyMapAttribute)
               .Value.ArrayValue.Should().Equal("datadog.local_root_span_id");

            AssertThreadContext(contexts.ThreadContext, contexts.Spans);
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

        private static void AssertThreadContext(
            OtelThreadContextReader.OtelThreadContextSnapshot context,
            IReadOnlyList<MockSpan> spans)
        {
            var rootSpan = spans.Single(span => span.Name == "otel-thread-context-root");
            var childSpan = spans.Single(span => span.Name == "otel-thread-context-child");

            childSpan.TraceId.Should().Be(rootSpan.TraceId);
            childSpan.ParentId.Should().Be(rootSpan.SpanId);

            var traceIdUpper = spans.Select(span => span.GetTag(Tags.Propagated.TraceIdUpper))
                                    .FirstOrDefault(value => value is not null)
                              ?? "0000000000000000";

            var samplingPriority = spans.Select(span => span.GetMetric(Metrics.SamplingPriority))
                                        .FirstOrDefault(priority => priority.HasValue);
            samplingPriority.Should().NotBeNull("the sample makes a sampling decision before activating the child span");

            context.RecordAddress.Should().BeGreaterThan(0);
            (context.RecordAddress % 64).Should().Be(0, "the native record must be cache-line aligned");
            context.Valid.Should().Be(1, "the child scope is active while the record is read");
            context.TraceId.Should().Be(traceIdUpper + rootSpan.TraceId.ToString("x16"));
            context.SpanId.Should().Be(childSpan.SpanId.ToString("x16"));
            context.TraceFlags.Should().Be(samplingPriority.Value > 0 ? (byte)1 : (byte)0);
            context.AttrsDataSize.Should().Be(18);
            context.Attributes.Should().ContainSingle();
            context.Attributes[0].KeyIndex.Should().Be(0);
            context.Attributes[0].Value.Should().Be(rootSpan.SpanId.ToString("x16"));
        }

        private static string GetManagedLogContent(string logDir)
        {
            var logFiles = Directory.GetFiles(logDir, "dotnet-tracer-managed-*.log");
            logFiles.Should().NotBeEmpty("the managed tracer must have written a log");

            return string.Concat(logFiles.Select(File.ReadAllText));
        }

        private static async Task<ulong> WaitForTlsAddressAsync(Process process, string path, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;

            while (DateTime.UtcNow < deadline)
            {
                if (File.Exists(path)
                 && ulong.TryParse(
                        File.ReadAllText(path),
                        NumberStyles.AllowHexSpecifier,
                        CultureInfo.InvariantCulture,
                        out var address))
                {
                    return address;
                }

                if (process.HasExited)
                {
                    throw new InvalidOperationException(
                        $"The sample exited with code {process.ExitCode} before publishing its otel_thread_ctx_v1 TLS address.");
                }

                await Task.Delay(50).ConfigureAwait(false);
            }

            throw new TimeoutException($"The sample did not publish its otel_thread_ctx_v1 TLS address within {timeout}.");
        }

        private async Task<ContextSnapshots> RunSampleAndReadContexts(MockTracerAgent agent)
        {
            var id = Guid.NewGuid().ToString("N");
            var releaseFile = Path.Combine(Path.GetTempPath(), $"otel-thread-context-release-{id}");
            var tlsAddressFile = Path.Combine(Path.GetTempPath(), $"otel-thread-context-address-{id}");
            SetEnvironmentVariable("DD_INTERNAL_TEST_FILE_TO_WATCH", releaseFile);
            SetEnvironmentVariable("DD_INTERNAL_TEST_OTEL_THREAD_CONTEXT_TLS_ADDRESS_FILE", tlsAddressFile);

            try
            {
                using var process = await StartSample(
                                        agent,
                                        arguments: "otel-thread-context",
                                        packageVersion: string.Empty,
                                        aspNetCorePort: 5000);
                using var processHelper = new ProcessHelper(process);

                OtelProcessContextReader.OtelProcessContextSnapshot processContext;
                OtelThreadContextReader.OtelThreadContextSnapshot threadContext;

                try
                {
                    var tlsAddress = await WaitForTlsAddressAsync(process, tlsAddressFile, TimeSpan.FromSeconds(15));
                    processContext = await OtelProcessContextReader.ReadAsync(process.Id, TimeSpan.FromSeconds(15));
                    threadContext = OtelThreadContextReader.Read(process.Id, tlsAddress);
                }
                finally
                {
                    File.WriteAllText(releaseFile, string.Empty);
                    WaitForProcessResult(processHelper);
                }

                var spans = await agent.WaitForSpansAsync(2);
                spans.Should().Contain(span => span.Name == "otel-thread-context-root");
                spans.Should().Contain(span => span.Name == "otel-thread-context-child");

                return new ContextSnapshots(processContext, threadContext, spans);
            }
            finally
            {
                File.Delete(releaseFile);
                File.Delete(tlsAddressFile);
                File.Delete(tlsAddressFile + ".tmp");
            }
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

        private sealed class ContextSnapshots
        {
            public ContextSnapshots(
                OtelProcessContextReader.OtelProcessContextSnapshot processContext,
                OtelThreadContextReader.OtelThreadContextSnapshot threadContext,
                IReadOnlyList<MockSpan> spans)
            {
                ProcessContext = processContext;
                ThreadContext = threadContext;
                Spans = spans;
            }

            public OtelProcessContextReader.OtelProcessContextSnapshot ProcessContext { get; }

            public OtelThreadContextReader.OtelThreadContextSnapshot ThreadContext { get; }

            public IReadOnlyList<MockSpan> Spans { get; }
        }
    }
}
