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

#nullable enable

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
        var updater = ConfigurationUpdater.Create("env", "version", 0);

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
    public async Task DynamicInstrumentationEnabled_FailedInitializationCanRetry()
    {
        var settings = DebuggerSettings.FromSource(
            new NameValueConfigurationSource(new() { { ConfigurationKeys.Debugger.DynamicInstrumentationEnabled, "1" }, }),
            NullConfigurationTelemetry.Instance);

        var discoveryService = new DiscoveryServiceFailsOnceMock();
        var rcmSubscriptionManagerMock = new RcmSubscriptionManagerMock();
        var lineProbeResolver = new LineProbeResolverMock();
        var snapshotUploader = new SnapshotUploaderMock();
        var logUploader = new LogUploaderMock();
        var diagnosticsUploader = new UploaderMock();
        var probeStatusPoller = new ProbeStatusPollerMock();
        var updater = ConfigurationUpdater.Create("env", "version", 0);

        var debugger = new DynamicInstrumentation(settings, discoveryService, rcmSubscriptionManagerMock, lineProbeResolver, snapshotUploader, logUploader, diagnosticsUploader, probeStatusPoller, updater, NoOpStatsd.Instance);
        try
        {
            debugger.Initialize();
            await debugger.GetInitializationTask();

            debugger.IsInitialized.Should().BeFalse("the first discovery subscription attempt fails");
            discoveryService.SubscribeCount.Should().Be(1);

            debugger.Initialize();
            await debugger.GetInitializationTask();

            discoveryService.SubscribeCount.Should().Be(2);
            debugger.IsInitialized.Should().BeTrue("the failed initialization attempt should not block a later retry");
            probeStatusPoller.Called.Should().BeTrue();
            snapshotUploader.Called.Should().BeTrue();
            diagnosticsUploader.Called.Should().BeTrue();
            rcmSubscriptionManagerMock.ProductKeys.Contains(RcmProducts.LiveDebugging).Should().BeTrue();
        }
        finally
        {
            debugger.Dispose();
        }
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
        var updater = ConfigurationUpdater.Create(string.Empty, string.Empty, 0);

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

    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutSeconds = 5)
    {
        var timeout = TimeSpan.FromSeconds(timeoutSeconds);
        var startTime = DateTime.UtcNow;

        while (!condition() && DateTime.UtcNow - startTime < timeout)
        {
            await Task.Delay(50);
        }
    }

    public class ProbeFileLoadingTests : IDisposable
    {
        private readonly List<string> _tempFiles = new();
        private readonly List<DynamicInstrumentation> _debuggers = new();

        public void Dispose()
        {
            foreach (var debugger in _debuggers)
            {
                debugger.Dispose();
            }

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
                    ""language"": ""dotnet"",
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
                    ""language"": ""dotnet"",
                    ""type"": ""METRIC_PROBE"",
                    ""where"": { ""typeName"": ""MyClass"", ""methodName"": ""MyMethod"" },
                    ""kind"": ""COUNT"",
                    ""metricName"": ""my.metric""
                },
                {
                    ""id"": ""span-1"",
                    ""language"": ""dotnet"",
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

        [Fact]
        public async Task ProbeFile_ValidProbe_AppliesInstrumentation()
        {
            var probeJson = @"[
                {
                    ""id"": ""applied-file-probe"",
                    ""language"": ""dotnet"",
                    ""type"": ""LOG_PROBE"",
                    ""where"": { ""sourceFile"": ""MyClass.cs"", ""lines"": [""25""] },
                    ""captureSnapshot"": true
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

            var lineProbeResolver = new LineProbeResolverMock();
            var probeStatusPoller = new ProbeStatusPollerMock();
            var debugger = CreateDebugger(settings, lineProbeResolver: lineProbeResolver, probeStatusPoller: probeStatusPoller);
            debugger.Initialize();

            await debugger.GetInitializationTask();

            lineProbeResolver.Called.Should().BeTrue("file probes should be applied to the owning DynamicInstrumentation instance");
            probeStatusPoller.Called.Should().BeTrue("applying a file probe should update probe statuses");
            GetCurrentConfiguration(debugger).LogProbes.Should().ContainSingle(probe => probe.Id == "applied-file-probe");
        }

        [Fact]
        public async Task ProbeFile_FilteredOutProbe_DoesNotStartRuntimeWithoutRcm()
        {
            var probeJson = @"[
                {
                    ""id"": ""filtered-file-probe"",
                    ""language"": ""java"",
                    ""type"": ""LOG_PROBE"",
                    ""where"": { ""sourceFile"": ""MyClass.cs"", ""lines"": [""25""] },
                    ""captureSnapshot"": true
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

            var lineProbeResolver = new LineProbeResolverMock();
            var probeStatusPoller = new ProbeStatusPollerMock();
            var debugger = CreateDebugger(settings, new DiscoveryServiceWithoutRcmMock(), lineProbeResolver, probeStatusPoller);
            try
            {
                debugger.Initialize();

                await WaitUntilAsync(() => GetFileProbes(debugger) is not null);

                GetCurrentConfiguration(debugger).LogProbes.Should().BeEmpty("non-dotnet file probes should be filtered out before starting the runtime");
                debugger.IsInitialized.Should().BeFalse("a file with no effective probes should not start DI without RCM");
                lineProbeResolver.Called.Should().BeFalse();
                probeStatusPoller.Called.Should().BeFalse();
            }
            finally
            {
                debugger.Dispose();
            }
        }

        [Fact]
        public void UpdateAddedProbeInstrumentations_RetryableLineProbeResolutionFailureReportsErrorStatus()
        {
            var settings = DebuggerSettings.FromSource(
                new NameValueConfigurationSource(new() { { ConfigurationKeys.Debugger.DynamicInstrumentationEnabled, "1" }, }),
                NullConfigurationTelemetry.Instance);
            var lineProbeResolver = new LineProbeResolverMock(
                new LineProbeResolveResult(
                    LiveProbeResolveStatus.Unbound,
                    LineProbeResolveReason.LoadedAssemblySourceFileMismatch,
                    "Source file location for probe did not uniquely match the PDB document path.",
                    ErrorKey: SourceMismatchKey(),
                    ErrorDetails: SourceMismatchDetails(),
                    ReportError: true));
            var probeStatusPoller = new ProbeStatusPollerMock();
            var debugger = CreateDebugger(settings, lineProbeResolver: lineProbeResolver, probeStatusPoller: probeStatusPoller);
            var probe = new LogProbe
            {
                Id = "line-probe-source-mismatch",
                Language = "dotnet",
                Where = new Where { SourceFile = "wrong/path/MyClass.cs", Lines = ["25"] },
                CaptureSnapshot = true
            };

            debugger.UpdateAddedProbeInstrumentations([probe]);

            var status = probeStatusPoller.UpdatedProbeStatuses.Should().ContainSingle().Subject.ProbeStatus;
            status.Status.Should().Be(global::Datadog.Trace.Debugger.Sink.Models.Status.ERROR);
            status.ErrorMessage.Should().Be("Source file location for probe did not uniquely match the PDB document path.");
        }

        [Fact]
        public void UpdateAddedProbeInstrumentations_RetryableLineProbeResolutionFailureBuildsErrorStatusFromKey()
        {
            var settings = DebuggerSettings.FromSource(
                new NameValueConfigurationSource(new() { { ConfigurationKeys.Debugger.DynamicInstrumentationEnabled, "1" }, }),
                NullConfigurationTelemetry.Instance);
            var lineProbeResolver = new LineProbeResolverMock(
                new LineProbeResolveResult(
                    LiveProbeResolveStatus.Unbound,
                    LineProbeResolveReason.LoadedAssemblySourceFileMismatch,
                    ErrorKey: SourceMismatchKey(),
                    ErrorDetails: SourceMismatchDetails(),
                    ReportError: true));
            var probeStatusPoller = new ProbeStatusPollerMock();
            var debugger = CreateDebugger(settings, lineProbeResolver: lineProbeResolver, probeStatusPoller: probeStatusPoller);
            var probe = new LogProbe
            {
                Id = "line-probe-source-mismatch",
                Language = "dotnet",
                Where = new Where { SourceFile = "wrong/path/MyClass.cs", Lines = ["25"] },
                CaptureSnapshot = true
            };

            debugger.UpdateAddedProbeInstrumentations([probe]);

            var status = probeStatusPoller.UpdatedProbeStatuses.Should().ContainSingle().Subject.ProbeStatus;
            status.Status.Should().Be(global::Datadog.Trace.Debugger.Sink.Models.Status.ERROR);
            status.ErrorMessage.Should().Contain("Source file location for probe did not uniquely match the PDB document path");
            status.ErrorMessage.Should().Contain("Fallback failure reason: NoQualifiedSuffixMatch");
        }

        [Fact]
        public void UpdateAddedProbeInstrumentations_RetryableLineProbeResolutionFailureKeepsProbeRetryable()
        {
            var settings = DebuggerSettings.FromSource(
                new NameValueConfigurationSource(new() { { ConfigurationKeys.Debugger.DynamicInstrumentationEnabled, "1" }, }),
                NullConfigurationTelemetry.Instance);
            var lineProbeResolver = new LineProbeResolverMock(
                new LineProbeResolveResult(
                    LiveProbeResolveStatus.Unbound,
                    LineProbeResolveReason.LoadedAssemblySourceFileMismatch,
                    "Source file location for probe did not uniquely match the PDB document path.",
                    ErrorKey: SourceMismatchKey(),
                    ErrorDetails: SourceMismatchDetails(),
                    ReportError: true));
            var probeStatusPoller = new ProbeStatusPollerMock();
            var debugger = CreateDebugger(settings, lineProbeResolver: lineProbeResolver, probeStatusPoller: probeStatusPoller);
            var probe = new LogProbe
            {
                Id = "line-probe-source-mismatch",
                Language = "dotnet",
                Where = new Where { SourceFile = "wrong/path/MyClass.cs", Lines = ["25"] },
                CaptureSnapshot = true
            };

            debugger.UpdateAddedProbeInstrumentations([probe]);

            lineProbeResolver.NextResult = new LineProbeResolveResult(
                LiveProbeResolveStatus.Bound,
                Diagnostics: new LineProbeResolutionDiagnostics(ProbeFile: "wrong/path/MyClass.cs", ProbeLine: 25));
            debugger.GetType()
                    .GetMethod("CheckUnboundProbes", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(debugger, [null, new AssemblyLoadEventArgs(typeof(DynamicInstrumentationTests).Assembly)]);

            probeStatusPoller.UpdatedProbeStatuses.Should().HaveCount(2);
            probeStatusPoller.UpdatedProbeStatuses[0].ProbeStatus.Status.Should().Be(global::Datadog.Trace.Debugger.Sink.Models.Status.ERROR);
            probeStatusPoller.UpdatedProbeStatuses[1].ProbeStatus.Should().Be(ProbeStatus.Default);
        }

        [Fact]
        public void CheckUnboundProbes_RetryableLineProbeResolutionFailureCanTransitionToErrorStatus()
        {
            var settings = DebuggerSettings.FromSource(
                new NameValueConfigurationSource(new() { { ConfigurationKeys.Debugger.DynamicInstrumentationEnabled, "1" }, }),
                NullConfigurationTelemetry.Instance);
            var lineProbeResolver = new LineProbeResolverMock(
                new LineProbeResolveResult(
                    LiveProbeResolveStatus.Unbound,
                    LineProbeResolveReason.AssemblyNotLoadedOrSymbolsUnavailable,
                    "Source file location for probe was not found in any currently loaded assembly."));
            var probeStatusPoller = new ProbeStatusPollerMock();
            var debugger = CreateDebugger(settings, lineProbeResolver: lineProbeResolver, probeStatusPoller: probeStatusPoller);
            var probe = new LogProbe
            {
                Id = "line-probe-source-mismatch",
                Language = "dotnet",
                Where = new Where { SourceFile = "wrong/path/MyClass.cs", Lines = ["25"] },
                CaptureSnapshot = true
            };

            debugger.UpdateAddedProbeInstrumentations([probe]);

            lineProbeResolver.NextResult = new LineProbeResolveResult(
                LiveProbeResolveStatus.Unbound,
                LineProbeResolveReason.LoadedAssemblySourceFileMismatch,
                ErrorKey: SourceMismatchKey(),
                ErrorDetails: SourceMismatchDetails(),
                ReportError: true);
            debugger.GetType()
                    .GetMethod("CheckUnboundProbes", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(debugger, [null, new AssemblyLoadEventArgs(typeof(DynamicInstrumentationTests).Assembly)]);

            probeStatusPoller.UpdatedProbeStatuses.Should().HaveCount(2);
            probeStatusPoller.UpdatedProbeStatuses[0].ProbeStatus.Status.Should().Be(global::Datadog.Trace.Debugger.Sink.Models.Status.RECEIVED);
            probeStatusPoller.UpdatedProbeStatuses[1].ProbeStatus.Status.Should().Be(global::Datadog.Trace.Debugger.Sink.Models.Status.ERROR);
            probeStatusPoller.UpdatedProbeStatuses[1].ProbeStatus.ErrorMessage.Should().Contain("Source file location for probe did not uniquely match the PDB document path");
            probeStatusPoller.UpdatedProbeStatuses[1].ProbeStatus.ErrorMessage.Should().Contain("Fallback failure reason: NoQualifiedSuffixMatch");
        }

        [Fact]
        public void CheckUnboundProbes_RetryableLineProbeResolutionFailureDoesNotRepeatSameErrorStatus()
        {
            var settings = DebuggerSettings.FromSource(
                new NameValueConfigurationSource(new() { { ConfigurationKeys.Debugger.DynamicInstrumentationEnabled, "1" }, }),
                NullConfigurationTelemetry.Instance);
            var lineProbeResolver = new LineProbeResolverMock(
                new LineProbeResolveResult(
                    LiveProbeResolveStatus.Unbound,
                    LineProbeResolveReason.AssemblyNotLoadedOrSymbolsUnavailable,
                    "Source file location for probe was not found in any currently loaded assembly."));
            var probeStatusPoller = new ProbeStatusPollerMock();
            var debugger = CreateDebugger(settings, lineProbeResolver: lineProbeResolver, probeStatusPoller: probeStatusPoller);
            var probe = new LogProbe
            {
                Id = "line-probe-source-mismatch",
                Language = "dotnet",
                Where = new Where { SourceFile = "wrong/path/MyClass.cs", Lines = ["25"] },
                CaptureSnapshot = true
            };

            debugger.UpdateAddedProbeInstrumentations([probe]);

            lineProbeResolver.NextResult = new LineProbeResolveResult(
                LiveProbeResolveStatus.Unbound,
                LineProbeResolveReason.LoadedAssemblySourceFileMismatch,
                ErrorKey: SourceMismatchKey(),
                ErrorDetails: SourceMismatchDetails(),
                ReportError: true);
            var checkUnboundProbes = debugger.GetType().GetMethod("CheckUnboundProbes", BindingFlags.Instance | BindingFlags.NonPublic)!;
            checkUnboundProbes.Invoke(debugger, [null, new AssemblyLoadEventArgs(typeof(DynamicInstrumentationTests).Assembly)]);
            checkUnboundProbes.Invoke(debugger, [null, new AssemblyLoadEventArgs(typeof(DynamicInstrumentationTests).Assembly)]);

            probeStatusPoller.UpdatedProbeStatuses.Should().HaveCount(2);
            probeStatusPoller.UpdatedProbeStatuses[0].ProbeStatus.Status.Should().Be(global::Datadog.Trace.Debugger.Sink.Models.Status.RECEIVED);
            probeStatusPoller.UpdatedProbeStatuses[1].ProbeStatus.Status.Should().Be(global::Datadog.Trace.Debugger.Sink.Models.Status.ERROR);
        }

        [Fact]
        public void CheckUnboundProbes_RetryableLineProbeResolutionFailureDoesNotRepeatWhenOnlyCountsChange()
        {
            var settings = DebuggerSettings.FromSource(
                new NameValueConfigurationSource(new() { { ConfigurationKeys.Debugger.DynamicInstrumentationEnabled, "1" }, }),
                NullConfigurationTelemetry.Instance);
            var lineProbeResolver = new LineProbeResolverMock(
                new LineProbeResolveResult(
                    LiveProbeResolveStatus.Unbound,
                    LineProbeResolveReason.AssemblyNotLoadedOrSymbolsUnavailable,
                    "Source file location for probe was not found in any currently loaded assembly."));
            var probeStatusPoller = new ProbeStatusPollerMock();
            var debugger = CreateDebugger(settings, lineProbeResolver: lineProbeResolver, probeStatusPoller: probeStatusPoller);
            var probe = new LogProbe
            {
                Id = "line-probe-source-mismatch",
                Language = "dotnet",
                Where = new Where { SourceFile = "wrong/path/MyClass.cs", Lines = ["25"] },
                CaptureSnapshot = true
            };

            debugger.UpdateAddedProbeInstrumentations([probe]);
            var checkUnboundProbes = debugger.GetType().GetMethod("CheckUnboundProbes", BindingFlags.Instance | BindingFlags.NonPublic)!;

            lineProbeResolver.NextResult = new LineProbeResolveResult(
                LiveProbeResolveStatus.Unbound,
                LineProbeResolveReason.LoadedAssemblySourceFileMismatch,
                ErrorKey: SourceMismatchKey(),
                ErrorDetails: SourceMismatchDetails(bestMatchingTrailingSegments: 1, qualifiedFallbackMatchCount: 0, sameFileNameMatchCount: 1),
                ReportError: true);
            checkUnboundProbes.Invoke(debugger, [null, new AssemblyLoadEventArgs(typeof(DynamicInstrumentationTests).Assembly)]);

            lineProbeResolver.NextResult = new LineProbeResolveResult(
                LiveProbeResolveStatus.Unbound,
                LineProbeResolveReason.LoadedAssemblySourceFileMismatch,
                ErrorKey: SourceMismatchKey(),
                ErrorDetails: SourceMismatchDetails(bestMatchingTrailingSegments: 3, qualifiedFallbackMatchCount: 2, sameFileNameMatchCount: 4),
                ReportError: true);
            checkUnboundProbes.Invoke(debugger, [null, new AssemblyLoadEventArgs(typeof(DynamicInstrumentationTests).Assembly)]);

            probeStatusPoller.UpdatedProbeStatuses.Should().HaveCount(2);
            probeStatusPoller.UpdatedProbeStatuses[0].ProbeStatus.Status.Should().Be(global::Datadog.Trace.Debugger.Sink.Models.Status.RECEIVED);
            probeStatusPoller.UpdatedProbeStatuses[1].ProbeStatus.Status.Should().Be(global::Datadog.Trace.Debugger.Sink.Models.Status.ERROR);
            probeStatusPoller.UpdatedProbeStatuses[1].ProbeStatus.ErrorMessage.Should().Contain("Same file name matches: 1");
        }

        [Fact]
        public void CheckUnboundProbes_RetryableLineProbeResolutionFailureReportsChangedErrorKey()
        {
            var settings = DebuggerSettings.FromSource(
                new NameValueConfigurationSource(new() { { ConfigurationKeys.Debugger.DynamicInstrumentationEnabled, "1" }, }),
                NullConfigurationTelemetry.Instance);
            var lineProbeResolver = new LineProbeResolverMock(
                new LineProbeResolveResult(
                    LiveProbeResolveStatus.Unbound,
                    LineProbeResolveReason.AssemblyNotLoadedOrSymbolsUnavailable,
                    "Source file location for probe was not found in any currently loaded assembly."));
            var probeStatusPoller = new ProbeStatusPollerMock();
            var debugger = CreateDebugger(settings, lineProbeResolver: lineProbeResolver, probeStatusPoller: probeStatusPoller);
            var probe = new LogProbe
            {
                Id = "line-probe-source-mismatch",
                Language = "dotnet",
                Where = new Where { SourceFile = "wrong/path/MyClass.cs", Lines = ["25"] },
                CaptureSnapshot = true
            };

            debugger.UpdateAddedProbeInstrumentations([probe]);
            var checkUnboundProbes = debugger.GetType().GetMethod("CheckUnboundProbes", BindingFlags.Instance | BindingFlags.NonPublic)!;

            lineProbeResolver.NextResult = new LineProbeResolveResult(
                LiveProbeResolveStatus.Unbound,
                LineProbeResolveReason.LoadedAssemblySourceFileMismatch,
                ErrorKey: SourceMismatchKey(),
                ErrorDetails: SourceMismatchDetails(),
                ReportError: true);
            checkUnboundProbes.Invoke(debugger, [null, new AssemblyLoadEventArgs(typeof(DynamicInstrumentationTests).Assembly)]);

            lineProbeResolver.NextResult = new LineProbeResolveResult(
                LiveProbeResolveStatus.Unbound,
                LineProbeResolveReason.LoadedAssemblySourceFileMismatch,
                ErrorKey: SourceMismatchKey(LineProbeFallbackFailureReason.AmbiguousQualifiedMatches),
                ErrorDetails: SourceMismatchDetails(LineProbeFallbackFailureReason.AmbiguousQualifiedMatches, bestMatchingTrailingSegments: 3, qualifiedFallbackMatchCount: 2, sameFileNameMatchCount: 2),
                ReportError: true);
            checkUnboundProbes.Invoke(debugger, [null, new AssemblyLoadEventArgs(typeof(DynamicInstrumentationTests).Assembly)]);

            probeStatusPoller.UpdatedProbeStatuses.Should().HaveCount(3);
            probeStatusPoller.UpdatedProbeStatuses[0].ProbeStatus.Status.Should().Be(global::Datadog.Trace.Debugger.Sink.Models.Status.RECEIVED);
            probeStatusPoller.UpdatedProbeStatuses[1].ProbeStatus.ErrorMessage.Should().Contain("Fallback failure reason: NoQualifiedSuffixMatch");
            probeStatusPoller.UpdatedProbeStatuses[2].ProbeStatus.ErrorMessage.Should().Contain("Fallback failure reason: AmbiguousQualifiedMatches");
        }

        [Fact]
        public void CheckUnboundProbes_TerminalLineProbeResolutionFailureStopsRetrying()
        {
            var settings = DebuggerSettings.FromSource(
                new NameValueConfigurationSource(new() { { ConfigurationKeys.Debugger.DynamicInstrumentationEnabled, "1" }, }),
                NullConfigurationTelemetry.Instance);
            var lineProbeResolver = new LineProbeResolverMock(
                new LineProbeResolveResult(
                    LiveProbeResolveStatus.Unbound,
                    LineProbeResolveReason.AssemblyNotLoadedOrSymbolsUnavailable,
                    "Source file location for probe was not found in any currently loaded assembly."));
            var probeStatusPoller = new ProbeStatusPollerMock();
            var debugger = CreateDebugger(settings, lineProbeResolver: lineProbeResolver, probeStatusPoller: probeStatusPoller);
            var probe = new LogProbe
            {
                Id = "line-probe-missing-pdb",
                Language = "dotnet",
                Where = new Where { SourceFile = "MyClass.cs", Lines = ["25"] },
                CaptureSnapshot = true
            };

            debugger.UpdateAddedProbeInstrumentations([probe]);

            lineProbeResolver.NextResult = new LineProbeResolveResult(
                LiveProbeResolveStatus.Error,
                LineProbeResolveReason.MissingPdb,
                "Failed to read from PDB");
            var checkUnboundProbes = debugger.GetType().GetMethod("CheckUnboundProbes", BindingFlags.Instance | BindingFlags.NonPublic)!;
            checkUnboundProbes.Invoke(debugger, [null, new AssemblyLoadEventArgs(typeof(DynamicInstrumentationTests).Assembly)]);
            checkUnboundProbes.Invoke(debugger, [null, new AssemblyLoadEventArgs(typeof(DynamicInstrumentationTests).Assembly)]);

            lineProbeResolver.CallCount.Should().Be(2);
            probeStatusPoller.UpdatedProbeStatuses.Should().HaveCount(2);
            probeStatusPoller.UpdatedProbeStatuses[0].ProbeStatus.Status.Should().Be(global::Datadog.Trace.Debugger.Sink.Models.Status.RECEIVED);
            probeStatusPoller.UpdatedProbeStatuses[1].ProbeStatus.Status.Should().Be(global::Datadog.Trace.Debugger.Sink.Models.Status.ERROR);
            probeStatusPoller.UpdatedProbeStatuses[1].ProbeStatus.ErrorMessage.Should().Be("Failed to read from PDB");
        }

        [Theory]
        [InlineData(null, false, "non-existent file")]
        [InlineData("{ invalid json }", true, "invalid json")]
        [InlineData("", true, "empty file")]
        [InlineData("[]", true, "empty array")]
        public async Task ProbeFile_InvalidOrMissingProbeFile_InitializationContinues(string? fileContent, bool createFile, string scenario)
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
                    ""language"": ""dotnet"",
                    ""type"": ""LOG_PROBE"",
                    ""where"": { ""sourceFile"": ""test.cs"", ""lines"": [""10""] },
                    ""captureSnapshot"": true
                },
                {
                    ""id"": ""invalid-no-type"",
                    ""where"": { ""sourceFile"": ""test.cs"", ""lines"": [""20""] }
                },
                {
                    ""id"": ""valid-2"",
                    ""language"": ""dotnet"",
                    ""type"": ""LOG_PROBE"",
                    ""where"": { ""sourceFile"": ""test.cs"", ""lines"": [""30""] },
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
                    ""language"": ""dotnet"",
                    ""type"": ""LOG_PROBE"",
                    ""where"": { ""sourceFile"": ""first.cs"", ""lines"": [""10""] },
                    ""template"": ""First occurrence"",
                    ""captureSnapshot"": true
                },
                {
                    ""id"": ""unique-id"",
                    ""language"": ""dotnet"",
                    ""type"": ""LOG_PROBE"",
                    ""where"": { ""sourceFile"": ""unique.cs"", ""lines"": [""20""] },
                    ""captureSnapshot"": true
                },
                {
                    ""id"": ""duplicate-id"",
                    ""language"": ""dotnet"",
                    ""type"": ""LOG_PROBE"",
                    ""where"": { ""sourceFile"": ""second.cs"", ""lines"": [""30""] },
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
            duplicateProbe!.Where.SourceFile.Should().Be("first.cs", "First occurrence should be kept");
            duplicateProbe.Template.Should().Be("First occurrence");
        }

        [Fact]
        public async Task ProbeFile_ValidFileWithoutRcm_InitializesWithoutWaitingForRcmTimeout()
        {
            var probeJson = @"[
                {
                    ""id"": ""file-only-id"",
                    ""language"": ""dotnet"",
                    ""type"": ""LOG_PROBE"",
                    ""where"": { ""sourceFile"": ""file-only.cs"", ""lines"": [""10""] },
                    ""captureSnapshot"": true
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

            var debugger = CreateDebugger(settings, new DiscoveryServiceWithoutRcmMock());
            debugger.Initialize();
            await WaitForInitializationAsync(debugger);

            debugger.IsInitialized.Should().BeTrue("file probes should not wait for the RCM availability timeout");
            GetFileProbes(debugger).Should().NotBeNull();
        }

        private static LineProbeResolveErrorKey SourceMismatchKey(LineProbeFallbackFailureReason fallbackFailureReason = LineProbeFallbackFailureReason.NoQualifiedSuffixMatch)
        {
            return new LineProbeResolveErrorKey(
                LineProbeResolveReason.LoadedAssemblySourceFileMismatch,
                fallbackFailureReason);
        }

        private static LineProbeResolveErrorDetails SourceMismatchDetails(
            LineProbeFallbackFailureReason fallbackFailureReason = LineProbeFallbackFailureReason.NoQualifiedSuffixMatch,
            int bestMatchingTrailingSegments = 1,
            int qualifiedFallbackMatchCount = 0,
            int sameFileNameMatchCount = 1)
        {
            return new LineProbeResolveErrorDetails(
                SourceMismatchKey(fallbackFailureReason),
                bestMatchingTrailingSegments,
                qualifiedFallbackMatchCount,
                sameFileNameMatchCount);
        }

        private static ProbeConfiguration? GetFileProbes(DynamicInstrumentation debugger)
        {
            var updater = GetConfigurationUpdater(debugger);
            var field = typeof(ConfigurationUpdater).GetField("_fileConfiguration", BindingFlags.Instance | BindingFlags.NonPublic);
            field.Should().NotBeNull();
            return (ProbeConfiguration?)field!.GetValue(updater);
        }

        private static ConfigurationUpdater GetConfigurationUpdater(DynamicInstrumentation debugger)
        {
            var field = typeof(DynamicInstrumentation).GetField("_configurationUpdater", BindingFlags.Instance | BindingFlags.NonPublic);
            field.Should().NotBeNull();
            return (ConfigurationUpdater)field!.GetValue(debugger)!;
        }

        private static ProbeConfiguration GetCurrentConfiguration(DynamicInstrumentation debugger)
        {
            var updater = GetConfigurationUpdater(debugger);
            var field = typeof(ConfigurationUpdater).GetField("_currentConfiguration", BindingFlags.Instance | BindingFlags.NonPublic);
            field.Should().NotBeNull();
            return (ProbeConfiguration)field!.GetValue(updater)!;
        }

        private string CreateTempProbeFile(string content)
        {
            var tempFile = Path.GetTempFileName();
            _tempFiles.Add(tempFile);
            File.WriteAllText(tempFile, content);
            return tempFile;
        }

        private DynamicInstrumentation CreateDebugger(
            DebuggerSettings settings,
            IDiscoveryService? discoveryService = null,
            LineProbeResolverMock? lineProbeResolver = null,
            ProbeStatusPollerMock? probeStatusPoller = null)
        {
            var rcmSubscriptionManagerMock = new RcmSubscriptionManagerMock();
            lineProbeResolver ??= new LineProbeResolverMock();
            var snapshotUploader = new SnapshotUploaderMock();
            var logUploader = new LogUploaderMock();
            var diagnosticsUploader = new UploaderMock();
            probeStatusPoller ??= new ProbeStatusPollerMock();

            var debugger = new DynamicInstrumentation(
                settings,
                discoveryService ?? new DiscoveryServiceMock(),
                rcmSubscriptionManagerMock,
                lineProbeResolver,
                snapshotUploader,
                logUploader,
                diagnosticsUploader,
                probeStatusPoller,
                ConfigurationUpdater.Create("env", "version", 0),
                global::Datadog.Trace.DogStatsd.NoOpStatsd.Instance,
                (_, _, _, _) => { });
            _debuggers.Add(debugger);
            return debugger;
        }
    }

    public class ProbeMergeUnitTests
    {
        [Fact]
        public void MergeProbes_FileAndRcm_UnionOfIds()
        {
            var fileProbes = new ProbeConfiguration
            {
                LogProbes = [new LogProbe { Id = "file-probe-1" }]
            };

            var rcmProbes = new ProbeConfiguration
            {
                LogProbes = [new LogProbe { Id = "rcm-probe-1" }]
            };

            var merged = ProbeConfigurationUtils.Merge(fileProbes, rcmProbes);

            merged.LogProbes.Select(p => p.Id).Should().BeEquivalentTo("file-probe-1", "rcm-probe-1");
        }

        [Fact]
        public void MergeProbes_DuplicateIds_RcmWins()
        {
            var fileProbes = new ProbeConfiguration
            {
                LogProbes =
                [
                    new LogProbe
                    {
                        Id = "shared-id",
                        Where = new Where { SourceFile = "file.cs", Lines = new[] { "10" } },
                        Template = "From file",
                        CaptureSnapshot = true,
                    }
                ]
            };

            var rcmProbes = new ProbeConfiguration
            {
                LogProbes =
                [
                    new LogProbe
                    {
                        Id = "shared-id",
                        Where = new Where { SourceFile = "rcm.cs", Lines = new[] { "99" } },
                        Template = "From RCM",
                        CaptureSnapshot = false,
                    }
                ]
            };

            var merged = ProbeConfigurationUtils.Merge(fileProbes, rcmProbes);

            merged.LogProbes.Should().HaveCount(1);

            var probe = merged.LogProbes[0];
            probe.Id.Should().Be("shared-id");
            probe.Where.SourceFile.Should().Be("rcm.cs");
            probe.Template.Should().Be("From RCM");
            probe.CaptureSnapshot.Should().BeFalse();
        }

        [Fact]
        public void RcmRemovalSuppressesFileProbeWithSameId()
        {
            var updater = ConfigurationUpdater.Create("env", "version", 0);

            updater.AcceptFile(
                new ProbeConfiguration
                {
                    LogProbes =
                    [
                        new LogProbe
                        {
                            Id = "shared-id",
                            Language = "dotnet",
                            Where = new Where { SourceFile = "file.cs", Lines = new[] { "10" } },
                            Template = "From file",
                        }
                    ]
                });

            updater.AcceptAdded(
                new ProbeConfiguration
                {
                    LogProbes =
                    [
                        new LogProbe
                        {
                            Id = "shared-id",
                            Language = "dotnet",
                            Where = new Where { SourceFile = "rcm.cs", Lines = new[] { "99" } },
                            Template = "From RCM",
                        }
                    ]
                });

            updater.AcceptRemoved([RemoteConfigurationPath.FromPath($"employee/{RcmProducts.LiveDebugging}/logProbe_shared-id/config")]);

            GetCurrentConfiguration(updater).LogProbes.Should().BeEmpty("an RCM removal for a probe ID should suppress the file probe with the same ID");
        }

        private static ProbeConfiguration GetCurrentConfiguration(ConfigurationUpdater updater)
        {
            var field = typeof(ConfigurationUpdater).GetField("_currentConfiguration", BindingFlags.Instance | BindingFlags.NonPublic);
            field.Should().NotBeNull();
            return (ProbeConfiguration)field!.GetValue(updater)!;
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
                    containerTagsHash: "containerTagsHash",
                    clientDropP0: false,
                    spanMetaStructs: true,
                    spanEvents: true));
        }

        public void RemoveSubscription(Action<AgentConfiguration> callback)
        {
        }

        public void SetCurrentConfigStateHash(string configStateHash)
        {
        }

        public Task DisposeAsync() => Task.CompletedTask;
    }

    private class DiscoveryServiceFailsOnceMock : IDiscoveryService
    {
        internal int SubscribeCount { get; private set; }

        public void SubscribeToChanges(Action<AgentConfiguration> callback)
        {
            SubscribeCount++;
            if (SubscribeCount == 1)
            {
                throw new InvalidOperationException("Simulated discovery subscription failure");
            }

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
                    containerTagsHash: "containerTagsHash",
                    clientDropP0: false,
                    spanMetaStructs: true,
                    spanEvents: true));
        }

        public void RemoveSubscription(Action<AgentConfiguration> callback)
        {
        }

        public void SetCurrentConfigStateHash(string configStateHash)
        {
        }

        public Task DisposeAsync() => Task.CompletedTask;
    }

    private class DiscoveryServiceWithoutRcmMock : IDiscoveryService
    {
        internal bool Called { get; private set; }

        public void SubscribeToChanges(Action<AgentConfiguration> callback)
        {
            Called = true;
            callback(
                new AgentConfiguration(
                    configurationEndpoint: null,
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
                    containerTagsHash: "containerTagsHash",
                    clientDropP0: false,
                    spanMetaStructs: true,
                    spanEvents: true));
        }

        public void RemoveSubscription(Action<AgentConfiguration> callback)
        {
        }

        public void SetCurrentConfigStateHash(string configStateHash)
        {
        }

        public Task DisposeAsync() => Task.CompletedTask;
    }

    private class RcmSubscriptionManagerMock : IRcmSubscriptionManager
    {
        public bool HasAnySubscription { get; }

        public ICollection<string> ProductKeys { get; } = new List<string>();

        public ISubscription? LastSubscription { get; private set; }

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

        public Task SendRequest(RcmClientTracer rcmTracer, Func<GetRcmRequest, Task<GetRcmResponse?>> callback)
        {
            throw new NotImplementedException();
        }
    }

    private class LineProbeResolverMock : ILineProbeResolver
    {
        private readonly LineProbeResolveResult _result;

        public LineProbeResolverMock()
            : this(new LineProbeResolveResult(LiveProbeResolveStatus.Error, LineProbeResolveReason.MissingPdb, "PDB not available in unit test"))
        {
        }

        public LineProbeResolverMock(LineProbeResolveResult result)
        {
            _result = result;
        }

        internal bool Called { get; private set; }

        internal int CallCount { get; private set; }

        internal LineProbeResolveResult? NextResult { get; set; }

        public LineProbeResolveResult TryResolveLineProbe(ProbeDefinition probe, out LineProbeResolver.BoundLineProbeLocation? location, LineProbeDiagnosticLevel diagnosticLevel = LineProbeDiagnosticLevel.Full)
        {
            Called = true;
            CallCount++;
            var result = NextResult ?? _result;
            if (result.Status == LiveProbeResolveStatus.Bound)
            {
                location = new LineProbeResolver.BoundLineProbeLocation(probe, Guid.NewGuid(), methodToken: 1, bytecodeOffset: 0, lineNumber: 25);
                return result;
            }

            location = null;
            return result;
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
        private readonly List<FetchProbeStatus> _updatedProbeStatuses = new();

        internal bool Called { get; private set; }

        internal IReadOnlyList<FetchProbeStatus> UpdatedProbeStatuses => _updatedProbeStatuses;

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
            _updatedProbeStatuses.AddRange(newProbeStatuses);
        }

        public void UpdateProbe(string probeId, FetchProbeStatus newProbeStatus)
        {
            Called = true;
            _updatedProbeStatuses.Add(newProbeStatus);
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
