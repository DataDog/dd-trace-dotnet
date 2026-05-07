// <copyright file="DebuggerManagerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Reflection;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Debugger;
using Datadog.Trace.Debugger.ExceptionAutoInstrumentation;
using Datadog.Trace.Debugger.Sink;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Debugger
{
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

        private static void SetSymbolsUploader(DebuggerManager manager, IDebuggerUploader uploader)
        {
            var property = typeof(DebuggerManager).GetProperty(nameof(DebuggerManager.SymbolsUploader), BindingFlags.Instance | BindingFlags.NonPublic);
            property.Should().NotBeNull();
            property!.SetValue(manager, uploader);
        }

        private static void InvokeDisableSymbolUploader(DebuggerManager manager)
        {
            var method = typeof(DebuggerManager).GetMethod("DisableSymbolUploader", BindingFlags.Instance | BindingFlags.NonPublic);
            method.Should().NotBeNull();
            method!.Invoke(manager, null);
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
    }
}
