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
        public void DisableSymbolUploaderResetsInitializationGateSoUploaderCanBeReenabled()
        {
            var manager = CreateDebuggerManager();
            var uploader = new DebuggerUploaderMock();
            SetSymDbInitialized(manager, 1);
            SetSymbolsUploader(manager, uploader);

            InvokeDisableSymbolUploader(manager);

            uploader.Disposed.Should().BeTrue();
            manager.SymbolsUploader.Should().BeNull();
            GetSymDbInitialized(manager).Should().Be(0);
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

                GetSymDbSubscriptionInitialized(manager).Should().Be(1);
                GetSymDbInitialized(manager).Should().Be(0);
                manager.SymbolsUploader.Should().BeNull();
            }
            finally
            {
                InvokeShutdownTasks(manager);
            }
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

        private static void SetSymDbInitialized(DebuggerManager manager, int value)
        {
            var field = GetSymDbInitializedField();
            field.SetValue(manager, value);
        }

        private static int GetSymDbInitialized(DebuggerManager manager)
        {
            var field = GetSymDbInitializedField();
            return (int)field.GetValue(manager)!;
        }

        private static FieldInfo GetSymDbInitializedField()
        {
            var field = typeof(DebuggerManager).GetField("_symDbInitialized", BindingFlags.Instance | BindingFlags.NonPublic);
            field.Should().NotBeNull();
            return field!;
        }

        private static int GetSymDbSubscriptionInitialized(DebuggerManager manager)
        {
            var field = typeof(DebuggerManager).GetField("_symDbSubscriptionInitialized", BindingFlags.Instance | BindingFlags.NonPublic);
            field.Should().NotBeNull();
            return (int)field!.GetValue(manager)!;
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

        private static void InvokeShutdownTasks(DebuggerManager manager)
        {
            var method = typeof(DebuggerManager).GetMethod("ShutdownTasks", BindingFlags.Instance | BindingFlags.NonPublic);
            method.Should().NotBeNull();
            method!.Invoke(manager, [null]);
        }

        private class DebuggerUploaderMock : IDebuggerUploader
        {
            public bool Disposed { get; private set; }

            public Task StartFlushingAsync()
            {
                return Task.CompletedTask;
            }

            public void Dispose()
            {
                Disposed = true;
            }
        }
    }
}
