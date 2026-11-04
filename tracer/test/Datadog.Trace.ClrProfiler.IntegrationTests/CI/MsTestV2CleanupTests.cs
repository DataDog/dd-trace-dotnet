// <copyright file="MsTestV2CleanupTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.Ci;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.CI;

[Trait("Area", "CIVisibility")]
[Trait("Category", "EndToEnd")]
[Trait("Category", "TestIntegrations")]
[Trait("RunOnWindows", "True")]
public class MsTestV2CleanupTests(ITestOutputHelper output) : TestingFrameworkEvpTest("MSTestTests", output)
{
    public static IEnumerable<object[]> CleanupVersions()
    {
        // Match the default sample: MSTest 4 on .NET 8+ / Framework, MSTest 3 on older .NET.
#if NET8_0_OR_GREATER || NETFRAMEWORK
        var defaultVersion = new Version(4, 4, 0);
#else
        var defaultVersion = new Version(3, 11, 1);
#endif
        foreach (var row in PackageVersions.MSTest)
        {
            var packageVersion = (string)row[0];
            var version = packageVersion.Length == 0 ? defaultVersion : new Version(packageVersion);
            // MSTest 4 removed EndOfAssembly.
            if (version < new Version(4, 0))
            {
                yield return [packageVersion, "EndOfAssembly"];
            }

            // Earlier adapters only support cleanup at the end of the assembly.
            if (version >= new Version(2, 2, 8))
            {
                yield return [packageVersion, "EndOfClass"];
            }
        }
    }

    [Theory]
    [MemberData(nameof(CleanupVersions))]
    [Flaky("Suite events are sometimes missing on Windows; cause under investigation", 5)]
    public async Task ClassCleanupFailureIsReportedBeforeTheSuiteCloses(string packageVersion, string cleanupLifecycle)
    {
        EnvironmentHelper.EnableDefaultTransport();
        EnvironmentHelper.DebugModeEnabled = true;
        InjectSession(out _, out _, out _, out _, out _, out _, out var runId);
        SetEnvironmentVariable(ConfigurationKeys.CIVisibility.FlakyRetryEnabled, "0");
        SetEnvironmentVariable("MSTEST_FAIL_CLASS_CLEANUP", "1");
        SetEnvironmentVariable("TESTINGPLATFORM_TELEMETRY_OPTOUT", "1");
        var logDirectory = Path.Combine(LogDirectory, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(logDirectory);
        SetEnvironmentVariable(ConfigurationKeys.LogDirectory, logDirectory);
        Output.WriteLine($"Cleanup diagnostics for run {runId}: {logDirectory}");
        var startedAt = DateTimeOffset.UtcNow;
        var suites = new ConcurrentQueue<JObject>();
        var eventTypes = new ConcurrentQueue<string>();
        var requests = new ConcurrentQueue<MockHttpRequest>();
        var processResults = new List<object>();
        using var agent = EnvironmentHelper.GetMockAgent();
        agent.RequestReceived += (_, e) =>
        {
            if (e.Value.PathAndQuery.EndsWith("api/v2/citestcycle"))
            {
                e.Value.Diagnostics = new MockHttpRequest.RequestDiagnostics();
                requests.Enqueue(e.Value);
            }
        };
        agent.EventPlatformProxyPayloadReceived += (_, e) =>
        {
            if (e.Value.PathAndQuery.EndsWith("api/v2/libraries/tests/services/setting"))
            {
                e.Value.Response = new MockTracerResponse(GetSettingsJson("false", "false", "false", "0"), 200);
            }
            else if (e.Value.PathAndQuery.EndsWith("api/v2/citestcycle"))
            {
                var payload = JsonConvert.DeserializeObject<MockCIVisibilityProtocol>(e.Value.BodyInJson);
                foreach (var testEvent in payload.Events)
                {
                    eventTypes.Enqueue(testEvent.Type);
                    if (testEvent.Type == SpanTypes.TestSuite)
                    {
                        suites.Enqueue(JObject.Parse(testEvent.Content.ToString()));
                    }
                }
            }
        };

        // Before 3.0.1, end-of-assembly cleanup failures are warnings and do not fail the run.
        var warningOnly = packageVersion.Length > 0 && new Version(packageVersion) < new Version(3, 0, 1) && cleanupLifecycle == "EndOfAssembly";
        var arguments = "--TestCaseFilter:FullyQualifiedName=Samples.MSTestTests.TestSuite.SimplePassTest";
#if NETFRAMEWORK
        // The VSTest launcher can be x64 even when this job uses the x86 profiler.
        arguments += " /Platform:" + EnvironmentTools.GetTestTargetPlatform();
#endif
        arguments += $" -- MSTest.ClassCleanupLifecycle={cleanupLifecycle}";
        Exception failure = null;
        try
        {
            using var result = await RunDotnetTestSampleAndWaitForExit(
                agent,
                arguments: arguments,
                packageVersion: packageVersion,
                expectedExitCode: warningOnly ? 0 : 1,
                onProcessExit: processResult =>
                {
                    // Capture before the helper checks the exit code or retries a known runtime startup failure.
                    var prefix = $"runner-{processResults.Count + 1}-{processResult.Process.Id}";
                    File.WriteAllText(Path.Combine(logDirectory, prefix + ".stdout.txt"), processResult.StandardOutput);
                    File.WriteAllText(Path.Combine(logDirectory, prefix + ".stderr.txt"), processResult.StandardError);
                    processResults.Add(new { ProcessId = processResult.Process.Id, processResult.ExitCode, FilePrefix = prefix });
                });

            // An aborted testhost can also return 1; report that startup failure before checking emitted events.
            result.StandardError.Should().NotContain("Test Run Aborted.");
            var suite = suites.ToArray().Should().ContainSingle().Subject;
            suite["meta"].Value<string>(TestTags.Status).Should().Be(TestTags.StatusFail);
            suite["meta"].Value<string>(Tags.ErrorMsg).Should().Contain("MSTest class cleanup failed.");
            var logs = Directory.GetFiles(logDirectory, "dotnet-tracer-managed-*.log");
            logs.Should().NotBeEmpty("the test must inspect the instrumented process logs");
            foreach (var log in logs)
            {
                File.ReadAllText(log).Should().NotContain("SetTag should not be called after the span was closed");
            }
        }
        catch (Exception ex)
        {
            failure = ex;
            throw;
        }
        finally
        {
            // Each invocation (including a failed flaky retry) has its own directory in the CI log artifact.
            // Delay hashing and file writes until after the sample exits so capture does not warm up gzip.
            try
            {
                using var receiver = Process.GetCurrentProcess();
                var requestDiagnostics = requests.ToArray().Select((request, index) => SaveRequest(logDirectory, index + 1, request)).ToArray();
                var eventCounts = eventTypes.ToArray().GroupBy(type => type).ToDictionary(group => group.Key, group => group.Count());
                var summary = new
                {
                    RunId = runId,
                    PackageVersion = packageVersion,
                    CleanupLifecycle = cleanupLifecycle,
                    StartedAt = startedAt,
                    CompletedAt = DateTimeOffset.UtcNow,
                    Failure = failure?.ToString(),
                    SampleTargetFramework = EnvironmentHelper.GetTargetFramework(),
                    SampleTargetPlatform = EnvironmentTools.GetTestTargetPlatform(),
                    Receiver = new
                    {
                        ProcessId = receiver.Id,
                        RuntimeInformation.FrameworkDescription,
                        Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
                        RuntimeInformation.OSDescription,
                        Environment.ProcessorCount,
                    },
                    RunnerProcesses = processResults,
                    EventCounts = eventCounts,
                    Requests = requestDiagnostics,
                };
                File.WriteAllText(Path.Combine(logDirectory, "cleanup-diagnostics.json"), JsonConvert.SerializeObject(summary, Formatting.Indented));
                Output.WriteLine($"Captured {requestDiagnostics.Length} citestcycle requests; event counts: {JsonConvert.SerializeObject(eventCounts)}. Diagnostics: {logDirectory}");
            }
            catch (Exception ex)
            {
                // Preserve the original assertion/runner failure if writing diagnostics also fails.
                Output.WriteLine($"Could not save cleanup diagnostics to {logDirectory}: {ex}");
            }
        }
    }

    private static object SaveRequest(string directory, int index, MockHttpRequest request)
    {
        var diagnostics = request.Diagnostics;
        var body = diagnostics.Body;
        var prefix = $"citestcycle-{index:D3}";
        string bodyFile = null;
        string sha256 = null;
        if (body.Array is not null)
        {
            bodyFile = prefix + ".body.bin";
            using (var file = File.Create(Path.Combine(directory, bodyFile)))
            {
                file.Write(body.Array, body.Offset, body.Count);
            }

            using var hash = SHA256.Create();
            sha256 = BitConverter.ToString(hash.ComputeHash(body.Array, body.Offset, body.Count)).Replace("-", string.Empty);
        }

        string decodedFile = null;
        if (diagnostics.DecodedBodyInJson is not null)
        {
            decodedFile = prefix + ".decoded.json";
            File.WriteAllText(Path.Combine(directory, decodedFile), diagnostics.DecodedBodyInJson);
        }

        return new
        {
            diagnostics.ReceivedAt,
            request.Method,
            request.PathAndQuery,
            request.ContentLength,
            Headers = request.Headers
                             .Where(header => header.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)
                                           || header.Key.Equals("Content-Encoding", StringComparison.OrdinalIgnoreCase)
                                           || header.Key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase)
                                           || header.Key.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase))
                             .ToDictionary(header => header.Key, header => string.Join(",", header.Value)),
            CapturedLength = body.Array is null ? (int?)null : body.Count,
            BodyFile = bodyFile,
            Sha256 = sha256,
            DecodedFile = decodedFile,
            diagnostics.ResponseStatusCode,
            Exception = diagnostics.Exception?.ToString(),
        };
    }
}
