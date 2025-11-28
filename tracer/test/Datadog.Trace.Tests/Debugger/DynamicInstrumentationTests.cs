// <copyright file="DynamicInstrumentationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Threading.Tasks;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Debugger;
using Datadog.Trace.Debugger.Configurations;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.Debugger.Models;
using Datadog.Trace.Debugger.ProbeStatuses;
using Datadog.Trace.Debugger.Sink;
using Datadog.Trace.DogStatsd;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.RemoteConfigurationManagement.Protocol;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Debugger;

public class DynamicInstrumentationTests
{
    [Fact]
    public async Task DynamicInstrumentationEnabled_ServicesCalled()
    {
        var settings = DebuggerSettings.FromSource(
            new NameValueConfigurationSource(new() { { ConfigurationKeys.Debugger.DynamicInstrumentationEnabled, "1" }, }),
            NullConfigurationTelemetry.Instance);

        var discoveryService = new DiscoveryServiceMock();
        var rcmSubscriptionManagerMock = new RcmSubscriptionManagerMock();
        var lineProbeResolver = new LineProbeResolverMock();
        var snapshotUploader = new SnapshotUploaderMock();
        var logUploader = new LogUploaderMock();
        var diagnosticsUploader = new UploaderMock();
        var probeStatusPoller = new ProbeStatusPollerMock();
        var updater = ConfigurationUpdater.Create("env", "version");

        var debugger = new DynamicInstrumentation(settings, discoveryService, rcmSubscriptionManagerMock, lineProbeResolver, snapshotUploader, logUploader, diagnosticsUploader, probeStatusPoller, updater, NoOpStatsd.Instance);
        debugger.Initialize();
        await WaitForInitializationAsync(debugger);

        discoveryService.Called.Should().BeTrue();
        debugger.IsInitialized.Should().BeTrue("Dynamic instrumentation should be initialized");

        probeStatusPoller.Called.Should().BeTrue();
        snapshotUploader.Called.Should().BeTrue();
        diagnosticsUploader.Called.Should().BeTrue();
        rcmSubscriptionManagerMock.ProductKeys.Contains(RcmProducts.LiveDebugging).Should().BeTrue();
    }

    [Fact]
    public void DynamicInstrumentationDisabled_ServicesNotCalled()
    {
        var settings = DebuggerSettings.FromSource(
            new NameValueConfigurationSource(new() { { ConfigurationKeys.Debugger.DynamicInstrumentationEnabled, "0" }, }),
            NullConfigurationTelemetry.Instance);

        var discoveryService = new DiscoveryServiceMock();
        var rcmSubscriptionManagerMock = new RcmSubscriptionManagerMock();
        var lineProbeResolver = new LineProbeResolverMock();
        var snapshotUploader = new SnapshotUploaderMock();
        var logUploader = new LogUploaderMock();
        var diagnosticsUploader = new UploaderMock();
        var probeStatusPoller = new ProbeStatusPollerMock();
        var updater = ConfigurationUpdater.Create(string.Empty, string.Empty);

        var debugger = new DynamicInstrumentation(settings, discoveryService, rcmSubscriptionManagerMock, lineProbeResolver, snapshotUploader, logUploader, diagnosticsUploader, probeStatusPoller, updater, NoOpStatsd.Instance);
        debugger.Initialize();
        lineProbeResolver.Called.Should().BeFalse();
        probeStatusPoller.Called.Should().BeFalse();
        snapshotUploader.Called.Should().BeFalse();
        diagnosticsUploader.Called.Should().BeFalse();
        probeStatusPoller.Called.Should().BeFalse();
        rcmSubscriptionManagerMock.ProductKeys.Contains(RcmProducts.LiveDebugging).Should().BeFalse();
    }

    private static async Task WaitForInitializationAsync(DynamicInstrumentation debugger, int timeoutSeconds = 5)
    {
        var timeout = TimeSpan.FromSeconds(timeoutSeconds);
        var startTime = DateTime.UtcNow;

        while (!debugger.IsInitialized && DateTime.UtcNow - startTime < timeout)
        {
            await Task.Delay(50);
        }
    }

    public class ProbeFileLoadingTests : IDisposable
    {
        private readonly List<string> _tempFiles = new();

        public void Dispose()
        {
            // Clean up temp files
            foreach (var file in _tempFiles)
            {
                try
                {
                    if (File.Exists(file))
                    {
                        File.Delete(file);
                    }
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        [Fact]
        public async Task ProbeFile_MultipleProbeTypes_LoadsAll()
        {
            var probeJson = @"[
                {
                    ""id"": ""100c9a5c-45ad-49dc-818b-c570d31e11d1"",
                    ""version"": 0,
                    ""type"": ""LOG_PROBE"",
                    ""where"": { ""sourceFile"": ""MyClass.cs"", ""lines"": [""25""] },
                    ""template"": ""Hello World"",
                    ""segments"": [{ ""str"": ""Hello World"" }],
                    ""captureSnapshot"": true,
                    ""capture"": { ""maxReferenceDepth"": 3 },
                    ""sampling"": { ""snapshotsPerSecond"": 100 }
                },
                {
                    ""id"": ""metric-1"",
                    ""type"": ""METRIC_PROBE"",
                    ""where"": { ""typeName"": ""MyClass"", ""methodName"": ""MyMethod"" },
                    ""kind"": ""COUNT"",
                    ""metricName"": ""my.metric""
                },
                {
                    ""id"": ""span-1"",
                    ""type"": ""SPAN_PROBE"",
                    ""where"": { ""typeName"": ""MyClass"", ""methodName"": ""MyMethod"" }
                }
            ]";

            var tempFile = CreateTempProbeFile(probeJson);

            var settings = DebuggerSettings.FromSource(
                new NameValueConfigurationSource(new()
                {
                    { ConfigurationKeys.Debugger.DynamicInstrumentationEnabled, "1" },
                    { ConfigurationKeys.Debugger.DynamicInstrumentationProbeFile, tempFile }
                }),
                NullConfigurationTelemetry.Instance);

            var debugger = CreateDebugger(settings);
            debugger.Initialize();

            var timeout = TimeSpan.FromSeconds(5);
            var startTime = DateTime.UtcNow;

            while (GetFileProbes(debugger) is null && DateTime.UtcNow - startTime < timeout)
            {
                await Task.Delay(50);
            }

            var fileProbes = GetFileProbes(debugger);
            fileProbes.Should().NotBeNull("Probe file should be loaded and applied");
            fileProbes!.LogProbes.Should().HaveCount(1);
            fileProbes.MetricProbes.Should().HaveCount(1);
            fileProbes.SpanProbes.Should().HaveCount(1);
        }

        [Theory]
        [InlineData(null, false, "non-existent file")]
        [InlineData("{ invalid json }", true, "invalid json")]
        [InlineData("", true, "empty file")]
        [InlineData("[]", true, "empty array")]
        public async Task ProbeFile_InvalidOrMissingProbeFile_InitializationContinues(string fileContent, bool createFile, string scenario)
        {
            string probeFilePath;

            if (createFile)
            {
                probeFilePath = CreateTempProbeFile(fileContent ?? string.Empty);
            }
            else
            {
                probeFilePath = "/nonexistent/path/probes.json";
            }

            var settings = DebuggerSettings.FromSource(
                new NameValueConfigurationSource(new()
                {
                    { ConfigurationKeys.Debugger.DynamicInstrumentationEnabled, "1" },
                    { ConfigurationKeys.Debugger.DynamicInstrumentationProbeFile, probeFilePath }
                }),
                NullConfigurationTelemetry.Instance);

            var debugger = CreateDebugger(settings);
            debugger.Initialize();
            await WaitForInitializationAsync(debugger);

            debugger.IsInitialized.Should().BeTrue($"Initialization should complete for scenario '{scenario}'");
            GetFileProbes(debugger).Should().BeNull($"No probes should be loaded for scenario '{scenario}'");
        }

        [Fact]
        public async Task ProbeFile_NoProbeFileConfigured_SkipsLoading()
        {
            var settings = DebuggerSettings.FromSource(
                new NameValueConfigurationSource(new()
                {
                    { ConfigurationKeys.Debugger.DynamicInstrumentationEnabled, "1" }
                }),
                NullConfigurationTelemetry.Instance);

            var debugger = CreateDebugger(settings);
            debugger.Initialize();
            await WaitForInitializationAsync(debugger);

            debugger.IsInitialized.Should().BeTrue("Initialization should complete normally");
            GetFileProbes(debugger).Should().BeNull("No probes should be loaded when no file is configured");
        }

        [Fact]
        public async Task ProbeFile_PartiallyValidProbes_LoadsValidOnes()
        {
            var probeJson = @"[
                {
                    ""id"": ""valid-1"",
                    ""type"": ""LOG_PROBE"",
                    ""where"": { ""sourceFile"": ""test.js"", ""lines"": [""10""] },
                    ""captureSnapshot"": true
                },
                {
                    ""id"": ""invalid-no-type"",
                    ""where"": { ""sourceFile"": ""test.js"", ""lines"": [""20""] }
                },
                {
                    ""id"": ""valid-2"",
                    ""type"": ""LOG_PROBE"",
                    ""where"": { ""sourceFile"": ""test.js"", ""lines"": [""30""] },
                    ""captureSnapshot"": false
                }
            ]";

            var tempFile = CreateTempProbeFile(probeJson);

            var settings = DebuggerSettings.FromSource(
                new NameValueConfigurationSource(new()
                {
                    { ConfigurationKeys.Debugger.DynamicInstrumentationEnabled, "1" },
                    { ConfigurationKeys.Debugger.DynamicInstrumentationProbeFile, tempFile }
                }),
                NullConfigurationTelemetry.Instance);

            var debugger = CreateDebugger(settings);
            debugger.Initialize();

            var timeout = TimeSpan.FromSeconds(5);
            var startTime = DateTime.UtcNow;

            while (GetFileProbes(debugger) is null && DateTime.UtcNow - startTime < timeout)
            {
                await Task.Delay(50);
            }

            var fileProbes = GetFileProbes(debugger);
            fileProbes.Should().NotBeNull("Valid probes should be loaded");
            fileProbes!.LogProbes.Should().HaveCount(2, "Only valid probes should be loaded");
        }

        [Fact]
        public async Task ProbeFile_DuplicateIdsWithinFile_KeepsFirstOccurrence()
        {
            var probeJson = @"[
                {
                    ""id"": ""duplicate-id"",
                    ""type"": ""LOG_PROBE"",
                    ""where"": { ""sourceFile"": ""first.js"", ""lines"": [""10""] },
                    ""template"": ""First occurrence"",
                    ""captureSnapshot"": true
                },
                {
                    ""id"": ""unique-id"",
                    ""type"": ""LOG_PROBE"",
                    ""where"": { ""sourceFile"": ""unique.js"", ""lines"": [""20""] },
                    ""captureSnapshot"": true
                },
                {
                    ""id"": ""duplicate-id"",
                    ""type"": ""LOG_PROBE"",
                    ""where"": { ""sourceFile"": ""second.js"", ""lines"": [""30""] },
                    ""template"": ""Second occurrence"",
                    ""captureSnapshot"": false
                }
            ]";

            var tempFile = CreateTempProbeFile(probeJson);

            var settings = DebuggerSettings.FromSource(
                new NameValueConfigurationSource(new()
                {
                    { ConfigurationKeys.Debugger.DynamicInstrumentationEnabled, "1" },
                    { ConfigurationKeys.Debugger.DynamicInstrumentationProbeFile, tempFile }
                }),
                NullConfigurationTelemetry.Instance);

            var debugger = CreateDebugger(settings);
            debugger.Initialize();

            var timeout = TimeSpan.FromSeconds(5);
            var startTime = DateTime.UtcNow;

            while (GetFileProbes(debugger) is null && DateTime.UtcNow - startTime < timeout)
            {
                await Task.Delay(50);
            }

            var fileProbes = GetFileProbes(debugger);
            fileProbes.Should().NotBeNull("Probes should be loaded");
            fileProbes!.LogProbes.Should().HaveCount(2, "Duplicate should be removed");

            // Verify the first occurrence is kept
            var duplicateProbe = fileProbes.LogProbes.FirstOrDefault(p => p.Id == "duplicate-id");
            duplicateProbe.Should().NotBeNull();
            duplicateProbe!.Where.SourceFile.Should().Be("first.js", "First occurrence should be kept");
            duplicateProbe.Template.Should().Be("First occurrence");
        }

        private static ProbeConfiguration GetFileProbes(DynamicInstrumentation debugger)
        {
            var field = typeof(DynamicInstrumentation).GetField("_fileProbes", BindingFlags.Instance | BindingFlags.NonPublic);
            field.Should().NotBeNull();
            return (ProbeConfiguration)field.GetValue(debugger);
        }

        private string CreateTempProbeFile(string content)
        {
            var tempFile = Path.GetTempFileName();
            _tempFiles.Add(tempFile);
            File.WriteAllText(tempFile, content);
            return tempFile;
        }

        private DynamicInstrumentation CreateDebugger(DebuggerSettings settings)
        {
            var discoveryService = new DiscoveryServiceMock();
            var rcmSubscriptionManagerMock = new RcmSubscriptionManagerMock();
            var lineProbeResolver = new LineProbeResolverMock();
            var snapshotUploader = new SnapshotUploaderMock();
            var logUploader = new LogUploaderMock();
            var diagnosticsUploader = new UploaderMock();
            var probeStatusPoller = new ProbeStatusPollerMock();

            return new DynamicInstrumentation(
                settings,
                discoveryService,
                rcmSubscriptionManagerMock,
                lineProbeResolver,
                snapshotUploader,
                logUploader,
                diagnosticsUploader,
                probeStatusPoller,
                ConfigurationUpdater.Create("env", "version"),
                new DogStatsd.NoOpStatsd());
        }
    }

    public class ProbeMergeUnitTests
    {
        [Fact]
        public void MergeProbes_FileAndRcm_UnionOfIds()
        {
            var debugger = CreateDebugger();

            var fileProbes = new[]
            {
                new LogProbe { Id = "file-probe-1" },
            };

            var rcmProbes = new[]
            {
                new LogProbe { Id = "rcm-probe-1" },
            };

            var merged = InvokeMergeProbes(debugger, fileProbes, rcmProbes);

            merged.Select(p => p.Id).Should().BeEquivalentTo("file-probe-1", "rcm-probe-1");
        }

        [Fact]
        public void MergeProbes_DuplicateIds_RcmWins()
        {
            var debugger = CreateDebugger();

            var fileProbes = new[]
            {
                new LogProbe
                {
                    Id = "shared-id",
                    Where = new Where { SourceFile = "file.js", Lines = new[] { "10" } },
                    Template = "From file",
                    CaptureSnapshot = true,
                },
            };

            var rcmProbes = new[]
            {
                new LogProbe
                {
                    Id = "shared-id",
                    Where = new Where { SourceFile = "rcm.js", Lines = new[] { "99" } },
                    Template = "From RCM",
                    CaptureSnapshot = false,
                },
            };

            var merged = InvokeMergeProbes(debugger, fileProbes, rcmProbes);

            merged.Should().HaveCount(1);

            var probe = merged[0];
            probe.Id.Should().Be("shared-id");
            probe.Where.SourceFile.Should().Be("rcm.js");
            probe.Template.Should().Be("From RCM");
            probe.CaptureSnapshot.Should().BeFalse();
        }

        private static DynamicInstrumentation CreateDebugger()
        {
            var settings = DebuggerSettings.FromSource(
                new NameValueConfigurationSource(new()
                {
                    { ConfigurationKeys.Debugger.DynamicInstrumentationEnabled, "1" },
                }),
                NullConfigurationTelemetry.Instance);

            var discoveryService = new DiscoveryServiceMock();
            var rcmSubscriptionManagerMock = new RcmSubscriptionManagerMock();
            var lineProbeResolver = new LineProbeResolverMock();
            var snapshotUploader = new SnapshotUploaderMock();
            var logUploader = new LogUploaderMock();
            var diagnosticsUploader = new UploaderMock();
            var probeStatusPoller = new ProbeStatusPollerMock();
            var updater = ConfigurationUpdater.Create("env", "version");

            return new DynamicInstrumentation(
                settings,
                discoveryService,
                rcmSubscriptionManagerMock,
                lineProbeResolver,
                snapshotUploader,
                logUploader,
                diagnosticsUploader,
                probeStatusPoller,
                updater,
                new DogStatsd.NoOpStatsd());
        }

        private static T[] InvokeMergeProbes<T>(DynamicInstrumentation debugger, T[] fileProbes, T[] rcmProbes)
            where T : ProbeDefinition
        {
            var method = typeof(DynamicInstrumentation).GetMethod("MergeProbes", BindingFlags.Instance | BindingFlags.NonPublic);
            method.Should().NotBeNull();

            return (T[])method!
                       .MakeGenericMethod(typeof(T))
                       .Invoke(debugger, [fileProbes, rcmProbes])!;
        }
    }

    private class DiscoveryServiceMock : IDiscoveryService
    {
        internal bool Called { get; private set; }

        public void SubscribeToChanges(Action<AgentConfiguration> callback)
        {
            Called = true;
            callback(
                new AgentConfiguration(
                    configurationEndpoint: "configurationEndpoint",
                    debuggerEndpoint: "debuggerEndpoint",
                    debuggerV2Endpoint: "debuggerV2Endpoint",
                    diagnosticsEndpoint: "diagnosticsEndpoint",
                    symbolDbEndpoint: "symbolDbEndpoint",
                    agentVersion: "agentVersion",
                    statsEndpoint: "traceStatsEndpoint",
                    dataStreamsMonitoringEndpoint: "dataStreamsMonitoringEndpoint",
                    eventPlatformProxyEndpoint: "eventPlatformProxyEndpoint",
                    telemetryProxyEndpoint: "telemetryProxyEndpoint",
                    tracerFlareEndpoint: "tracerFlareEndpoint",
                    clientDropP0: false,
                    spanMetaStructs: true,
                    spanEvents: true));
        }

        public void RemoveSubscription(Action<AgentConfiguration> callback)
        {
        }

        public Task DisposeAsync() => Task.CompletedTask;
    }

    private class RcmSubscriptionManagerMock : IRcmSubscriptionManager
    {
        public bool HasAnySubscription { get; }

        public ICollection<string> ProductKeys { get; } = new List<string>();

        public ISubscription LastSubscription { get; private set; }

        public void SubscribeToChanges(ISubscription subscription)
        {
            LastSubscription = subscription;
            foreach (var productKey in subscription.ProductKeys)
            {
                ProductKeys.Add(productKey);
            }
        }

        public void Replace(ISubscription oldSubscription, ISubscription newSubscription)
        {
            foreach (var productKey in oldSubscription.ProductKeys)
            {
                ProductKeys.Remove(productKey);
            }

            foreach (var productKey in newSubscription.ProductKeys)
            {
                ProductKeys.Add(productKey);
            }
        }

        public void Unsubscribe(ISubscription subscription)
        {
            foreach (var productKey in subscription.ProductKeys)
            {
                ProductKeys.Remove(productKey);
            }
        }

        public void SetCapability(BigInteger index, bool available)
        {
            throw new NotImplementedException();
        }

        public byte[] GetCapabilities()
        {
            throw new NotImplementedException();
        }

        public Task SendRequest(RcmClientTracer rcmTracer, Func<GetRcmRequest, Task<GetRcmResponse>> callback)
        {
            throw new NotImplementedException();
        }
    }

    private class LineProbeResolverMock : ILineProbeResolver
    {
        internal bool Called { get; private set; }

        public LineProbeResolveResult TryResolveLineProbe(ProbeDefinition probe, out LineProbeResolver.BoundLineProbeLocation location)
        {
            throw new NotImplementedException();
        }
    }

    private class UploaderMock : IDebuggerUploader
    {
        internal bool Called { get; private set; }

        public Task StartFlushingAsync()
        {
            Called = true;
            return Task.CompletedTask;
        }

        public void Dispose()
        {
        }
    }

    private class SnapshotUploaderMock : UploaderMock, ISnapshotUploader
    {
        public void Add(string probeId, string snapshot)
        {
        }
    }

    private class LogUploaderMock : UploaderMock, ISnapshotUploader
    {
        public void Add(string probeId, string snapshot)
        {
        }
    }

    private class ProbeStatusPollerMock : IProbeStatusPoller
    {
        internal bool Called { get; private set; }

        public void StartPolling()
        {
            Called = true;
        }

        public void AddProbes(FetchProbeStatus[] newProbes)
        {
            Called = true;
        }

        public void RemoveProbes(string[] removeProbes)
        {
            Called = true;
        }

        public void UpdateProbes(string[] probeIds, FetchProbeStatus[] newProbeStatuses)
        {
            Called = true;
        }

        public void UpdateProbe(string probeId, FetchProbeStatus newProbeStatus)
        {
            Called = true;
        }

        public string[] GetBoundedProbes()
        {
            Called = true;
            return [];
        }

        public void Dispose()
        {
        }
    }
}
