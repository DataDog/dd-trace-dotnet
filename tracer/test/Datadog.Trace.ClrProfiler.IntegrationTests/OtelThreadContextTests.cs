// <copyright file="OtelThreadContextTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

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
    /// tests; what can only be verified here is that the native <c>otel_thread_ctx_v1</c> symbol resolves in
    /// a real instrumented process, that installing the record succeeds on every thread, and that turning
    /// the feature on does not disturb tracing. See docs/OTelContextPropagation.md.
    /// </summary>
    public class OtelThreadContextTests : TestHelper
    {
        public OtelThreadContextTests(ITestOutputHelper output)
            : base("Console", output)
        {
            SetServiceVersion("1.0.0");
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
            using var processResult = await RunSampleAndWaitForExit(agent, arguments: "traces 1");

            var spans = await agent.WaitForSpansAsync(1);
            spans.Should().NotBeEmpty("turning on thread context publication must not affect tracing");

            // The publisher latches itself off and logs a single warning the first time anything fails -
            // resolving the slot, or writing the record - so the absence of that warning is what tells us
            // the native symbol resolved and every thread installed its record.
            AssertNoThreadContextFailures(logDir);

            // Readers ignore the thread context records entirely unless the process context advertises
            // threadlocal.schema_version, so the announcement is the half that makes the feature visible.
            GetManagedLogContent(logDir)
                .Should().Contain("Announced the OpenTelemetry thread context schema");
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "False")]
        public async Task IsInertWhenDisabled()
        {
            SkipUnlessLinux();

            var logDir = SetLogDirectory();

            using var agent = EnvironmentHelper.GetMockAgent();
            using var processResult = await RunSampleAndWaitForExit(agent, arguments: "traces 1");

            var spans = await agent.WaitForSpansAsync(1);
            spans.Should().NotBeEmpty();

            // nothing should be logged at all when the feature is off, not even the "unavailable" notice
            GetManagedLogContent(logDir).Should().NotContain("thread context");
        }

        private static void SkipUnlessLinux() => SkipOn.AllExcept(SkipOn.PlatformValue.Linux);

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

        private string SetLogDirectory([CallerMemberName] string testName = null)
        {
            var logDir = Path.Combine(LogDirectory, testName);
            Directory.CreateDirectory(logDir);
            SetEnvironmentVariable(ConfigurationKeys.LogDirectory, logDir);
            return logDir;
        }
    }
}
