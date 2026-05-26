// <copyright file="DebuggerManagerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Debugger;
using Datadog.Trace.Debugger.ExceptionAutoInstrumentation;
using Datadog.Trace.Debugger.RateLimiting;
using Datadog.Trace.Debugger.Sink;
using Datadog.Trace.Debugger.Snapshots;
using Datadog.Trace.Logging;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Debugger
{
    [Collection(nameof(RedactionTests))]
    public class DebuggerManagerTests
    {
        [Fact]
        public void DisableSymbolUploaderDisposesUploaderSoItCanBeReenabled()
        {
            var manager = CreateDebuggerManager();
            var uploader = new DebuggerUploaderMock();
            SetSymbolsUploader(manager, uploader);

            InvokeDisableSymbolUploader(manager);

            uploader.Disposed.Should().BeTrue();
            manager.SymbolsUploader.Should().BeNull();
        }

        [Fact]
        public async Task UpdateConfigurationWithRemoteConfigurationAvailableDoesNotInitializeSymbolUploaderBeforeSymDbSignal()
        {
            var manager = CreateDebuggerManager();
            var tracerSettings = TracerSettings.Create(new()
            {
                { ConfigurationKeys.Rcm.RemoteConfigurationEnabled, "true" },
            });
            var debuggerSettings = new DebuggerSettings(
                new NameValueConfigurationSource(new()
                {
                    { ConfigurationKeys.Debugger.DynamicInstrumentationEnabled, "false" },
                    { ConfigurationKeys.Debugger.CodeOriginForSpansEnabled, "false" },
                    { ConfigurationKeys.Debugger.SymbolDatabaseUploadEnabled, "true" },
                }),
                NullConfigurationTelemetry.Instance);

            tracerSettings.IsRemoteConfigurationAvailable.Should().BeTrue();

            try
            {
                await manager.UpdateConfiguration(tracerSettings, debuggerSettings);

                manager.SymbolsUploader.Should().BeNull();
            }
            finally
            {
                InvokeShutdownTasks(manager);
            }
        }

        [Fact]
        public void SymDbRemoteConfigurationEnableReturnsEarlyWhenUploaderAlreadyExists()
        {
            var manager = CreateDebuggerManager();
            var uploader = new DebuggerUploaderMock();
            SetSymbolsUploader(manager, uploader);
            var tracerSettings = TracerSettings.Create(new()
            {
                { ConfigurationKeys.Rcm.RemoteConfigurationEnabled, "true" },
            });
            var debuggerSettings = new DebuggerSettings(
                new NameValueConfigurationSource(new()
                {
                    { ConfigurationKeys.Debugger.SymbolDatabaseUploadEnabled, "true" },
                }),
                NullConfigurationTelemetry.Instance);

            try
            {
                InvokeOnSymbolDatabaseRemoteConfiguration(manager, tracerSettings, debuggerSettings, uploadSymbols: true);

                manager.SymbolsUploader.Should().BeSameAs(uploader);
                uploader.Started.Should().BeFalse();

                InvokeDisableSymbolUploader(manager);

                uploader.Disposed.Should().BeTrue();
                manager.SymbolsUploader.Should().BeNull();
            }
            finally
            {
                InvokeShutdownTasks(manager);
            }
        }

        [Fact]
        public void SymbolUploaderStartFailureClearsOnlyFailedUploader()
        {
            var manager = CreateDebuggerManager();
            var failedUploader = new DebuggerUploaderMock();
            var replacementUploader = new DebuggerUploaderMock();
            SetSymbolsUploader(manager, failedUploader);

            InvokeHandleSymbolUploaderStartFailure(manager, failedUploader);

            failedUploader.Disposed.Should().BeTrue();
            manager.SymbolsUploader.Should().BeNull();

            SetSymbolsUploader(manager, replacementUploader);
            InvokeHandleSymbolUploaderStartFailure(manager, failedUploader);

            replacementUploader.Disposed.Should().BeFalse();
            manager.SymbolsUploader.Should().BeSameAs(replacementUploader);
        }

        [Fact]
        public void EnsureSnapshotPipelineConfiguredConfiguresRedactionOnce()
        {
            Redaction.Instance.ResetInstance();
            var manager = CreateDebuggerManager();
            var firstSettings = CreateDebuggerSettings(redactedIdentifier: "reviewconfigone");
            var secondSettings = CreateDebuggerSettings(redactedIdentifier: "reviewconfigtwo");

            try
            {
                InvokeEnsureSnapshotPipelineConfigured(manager, firstSettings);
                InvokeEnsureSnapshotPipelineConfigured(manager, secondSettings);

                GetSnapshotPipelineConfigured(manager).Should().Be(1);
                Redaction.Instance.IsRedactedKeyword("reviewconfigone").Should().BeTrue();
                Redaction.Instance.IsRedactedKeyword("reviewconfigtwo").Should().BeFalse();
            }
            finally
            {
                Redaction.Instance.ResetInstance();
            }
        }

        [Fact]
        public void EnsureSnapshotPipelineConfiguredCanRetryAfterFailure()
        {
            Redaction.Instance.ResetInstance();
            var manager = CreateDebuggerManager();
            var settings = CreateDebuggerSettings(redactedIdentifier: "reviewretrytoken");

            try
            {
                var act = () => InvokeEnsureSnapshotPipelineConfigured(manager, null!);

                act.Should().Throw<TargetInvocationException>();
                GetSnapshotPipelineConfigured(manager).Should().Be(0);

                InvokeEnsureSnapshotPipelineConfigured(manager, settings);

                GetSnapshotPipelineConfigured(manager).Should().Be(1);
                Redaction.Instance.IsRedactedKeyword("reviewretrytoken").Should().BeTrue();
            }
            finally
            {
                Redaction.Instance.ResetInstance();
            }
        }

        [Fact]
        public void EnsureSnapshotPipelineConfiguredWaitsForInProgressConfiguration()
        {
            Redaction.Instance.ResetInstance();
            var manager = CreateDebuggerManager();
            var settings = CreateDebuggerSettings(redactedIdentifier: "reviewblockedtoken");
            var syncLock = GetSyncLock(manager);
            using var blocked = new ManualResetEventSlim();
            Exception? threadException = null;

            Monitor.Enter(syncLock);
            try
            {
                var configureThread = new Thread(
                    () =>
                    {
                        try
                        {
                            blocked.Set();
                            InvokeEnsureSnapshotPipelineConfigured(manager, settings);
                        }
                        catch (Exception ex)
                        {
                            threadException = ex;
                        }
                    });
                configureThread.Start();

                blocked.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue();
                configureThread.IsAlive.Should().BeTrue();
                GetSnapshotPipelineConfigured(manager).Should().Be(0);

                Monitor.Exit(syncLock);
                syncLock = null;

                configureThread.Join(TimeSpan.FromSeconds(5)).Should().BeTrue();
                threadException.Should().BeNull();

                GetSnapshotPipelineConfigured(manager).Should().Be(1);
                Redaction.Instance.IsRedactedKeyword("reviewblockedtoken").Should().BeTrue();
            }
            finally
            {
                if (syncLock is not null)
                {
                    Monitor.Exit(syncLock);
                }

                Redaction.Instance.ResetInstance();
            }
        }

        [Fact]
        public void EnsureGlobalRateLimiterCreatesProcessWideLimiterLazily()
        {
            var manager = CreateDebuggerManager();

            GetGlobalRateLimiter(manager).Should().BeNull();

            var limiter = InvokeEnsureGlobalRateLimiter(manager);

            limiter.Should().NotBeNull();
            GetGlobalRateLimiter(manager).Should().BeSameAs(limiter);
        }

        [Fact]
        public void EnsureGlobalRateLimiterReturnsSameInstanceAcrossRepeatedRequests()
        {
            var manager = CreateDebuggerManager();

            var first = InvokeEnsureGlobalRateLimiter(manager);
            var second = InvokeEnsureGlobalRateLimiter(manager);

            second.Should().BeSameAs(first);
        }

        [Fact]
        public void DisableDynamicInstrumentationDoesNotDisposeGlobalRateLimiter()
        {
            var manager = CreateDebuggerManager();
            var limiter = CreateGlobalRateLimiter(out var samplerFactory);
            limiter.Initialize();
            SetGlobalRateLimiter(manager, limiter);
            var activeSampler = samplerFactory.Samplers[0];

            InvokeDisableDynamicInstrumentation(manager, dynamicallyDisabled: true);

            GetGlobalRateLimiter(manager).Should().BeSameAs(limiter);
            activeSampler.DisposeCallCount.Should().Be(0);
        }

        [Fact]
        public void ShutdownTasksDisposesGlobalRateLimiter()
        {
            var manager = CreateDebuggerManager();
            var limiter = CreateGlobalRateLimiter(out var samplerFactory);
            limiter.Initialize();
            SetGlobalRateLimiter(manager, limiter);
            var activeSampler = samplerFactory.Samplers[0];

            InvokeShutdownTasks(manager);

            activeSampler.DisposeCallCount.Should().Be(1);
            limiter.SetRate(42);
            samplerFactory.RequestedRates.Should().Equal(DebuggerGlobalRateLimiter.DefaultSnapshotSamplesPerSecond);
        }

        private static DebuggerManager CreateDebuggerManager()
        {
            var constructor = typeof(DebuggerManager).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                [typeof(DebuggerSettings), typeof(ExceptionReplaySettings)],
                modifiers: null);
            constructor.Should().NotBeNull();

            var debuggerSettings = new DebuggerSettings(NullConfigurationSource.Instance, NullConfigurationTelemetry.Instance);
            var exceptionReplaySettings = new ExceptionReplaySettings(NullConfigurationSource.Instance, NullConfigurationTelemetry.Instance);
            return (DebuggerManager)constructor!.Invoke([debuggerSettings, exceptionReplaySettings]);
        }

        private static DebuggerSettings CreateDebuggerSettings(string redactedIdentifier)
        {
            return new DebuggerSettings(
                new NameValueConfigurationSource(new()
                {
                    { ConfigurationKeys.Debugger.RedactedIdentifiers, redactedIdentifier },
                }),
                NullConfigurationTelemetry.Instance);
        }

        private static int GetSnapshotPipelineConfigured(DebuggerManager manager)
        {
            var field = typeof(DebuggerManager).GetField("_snapshotPipelineConfigured", BindingFlags.Instance | BindingFlags.NonPublic);
            field.Should().NotBeNull();
            return (int)field!.GetValue(manager)!;
        }

        private static object GetSyncLock(DebuggerManager manager)
        {
            var field = typeof(DebuggerManager).GetField("_syncLock", BindingFlags.Instance | BindingFlags.NonPublic);
            field.Should().NotBeNull();
            return field!.GetValue(manager)!;
        }

        private static DebuggerGlobalRateLimiter? GetGlobalRateLimiter(DebuggerManager manager)
        {
            var field = typeof(DebuggerManager).GetField("_globalRateLimiter", BindingFlags.Instance | BindingFlags.NonPublic);
            field.Should().NotBeNull();
            return (DebuggerGlobalRateLimiter?)field!.GetValue(manager);
        }

        private static DebuggerGlobalRateLimiter InvokeEnsureGlobalRateLimiter(DebuggerManager manager)
        {
            var method = typeof(DebuggerManager).GetMethod("EnsureGlobalRateLimiter", BindingFlags.Instance | BindingFlags.NonPublic);
            method.Should().NotBeNull();
            return (DebuggerGlobalRateLimiter)method!.Invoke(manager, null)!;
        }

        private static void SetSymbolsUploader(DebuggerManager manager, IDebuggerUploader uploader)
        {
            var property = typeof(DebuggerManager).GetProperty(nameof(DebuggerManager.SymbolsUploader), BindingFlags.Instance | BindingFlags.NonPublic);
            property.Should().NotBeNull();
            property!.SetValue(manager, uploader);
        }

        private static void SetGlobalRateLimiter(DebuggerManager manager, DebuggerGlobalRateLimiter limiter)
        {
            var field = typeof(DebuggerManager).GetField("_globalRateLimiter", BindingFlags.Instance | BindingFlags.NonPublic);
            field.Should().NotBeNull();
            field!.SetValue(manager, limiter);
        }

        private static DebuggerGlobalRateLimiter CreateGlobalRateLimiter(out RecordingSamplerFactory samplerFactory)
        {
            samplerFactory = new RecordingSamplerFactory();
            return new DebuggerGlobalRateLimiter(samplerFactory.Create, new NullLogRateLimiter());
        }

        private static void InvokeEnsureSnapshotPipelineConfigured(DebuggerManager manager, DebuggerSettings settings)
        {
            var method = typeof(DebuggerManager).GetMethod("EnsureSnapshotPipelineConfigured", BindingFlags.Instance | BindingFlags.NonPublic);
            method.Should().NotBeNull();
            method!.Invoke(manager, [settings]);
        }

        private static void InvokeDisableSymbolUploader(DebuggerManager manager)
        {
            var method = typeof(DebuggerManager).GetMethod("DisableSymbolUploader", BindingFlags.Instance | BindingFlags.NonPublic);
            method.Should().NotBeNull();
            method!.Invoke(manager, null);
        }

        private static void InvokeDisableDynamicInstrumentation(DebuggerManager manager, bool dynamicallyDisabled)
        {
            var method = typeof(DebuggerManager).GetMethod("DisableDynamicInstrumentation", BindingFlags.Instance | BindingFlags.NonPublic);
            method.Should().NotBeNull();
            method!.Invoke(manager, [dynamicallyDisabled]);
        }

        private static void InvokeOnSymbolDatabaseRemoteConfiguration(DebuggerManager manager, TracerSettings tracerSettings, DebuggerSettings debuggerSettings, bool uploadSymbols)
        {
            var method = typeof(DebuggerManager).GetMethod("OnSymbolDatabaseRemoteConfiguration", BindingFlags.Instance | BindingFlags.NonPublic);
            method.Should().NotBeNull();
            method!.Invoke(manager, [tracerSettings, debuggerSettings, uploadSymbols]);
        }

        private static void InvokeHandleSymbolUploaderStartFailure(DebuggerManager manager, IDebuggerUploader failedUploader)
        {
            var method = typeof(DebuggerManager).GetMethod("HandleSymbolUploaderStartFailure", BindingFlags.Instance | BindingFlags.NonPublic);
            method.Should().NotBeNull();
            method!.Invoke(manager, [failedUploader, null]);
        }

        private static void InvokeShutdownTasks(DebuggerManager manager)
        {
            var method = typeof(DebuggerManager).GetMethod("ShutdownTasks", BindingFlags.Instance | BindingFlags.NonPublic);
            method.Should().NotBeNull();
            method!.Invoke(manager, [null]);
        }

        private class DebuggerUploaderMock : IDebuggerUploader
        {
            public bool Disposed { get; private set; }

            public bool Started { get; private set; }

            public Task StartFlushingAsync()
            {
                Started = true;
                return Task.CompletedTask;
            }

            public void Dispose()
            {
                Disposed = true;
            }
        }

        private sealed class RecordingSamplerFactory
        {
            public List<int> RequestedRates { get; } = [];

            public List<TestAdaptiveSampler> Samplers { get; } = [];

            public IAdaptiveSampler Create(int samplesPerSecond)
            {
                RequestedRates.Add(samplesPerSecond);
                var sampler = new TestAdaptiveSampler();
                Samplers.Add(sampler);
                return sampler;
            }
        }

        private sealed class TestAdaptiveSampler : IAdaptiveSampler
        {
            public int DisposeCallCount { get; private set; }

            public bool Sample() => true;

            public bool Keep() => true;

            public bool Drop() => false;

            public double NextDouble() => 0;

            public void Dispose()
            {
                DisposeCallCount++;
            }
        }
    }
}
