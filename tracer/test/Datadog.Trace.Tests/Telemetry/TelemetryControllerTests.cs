// <copyright file="TelemetryControllerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Vendors.Serilog.Events;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Tests.Telemetry
{
    public class TelemetryControllerTests
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<TelemetryControllerTests>();

        private readonly ITestOutputHelper _testOutputHelper;
        private readonly TimeSpan _refreshInterval = TimeSpan.FromMilliseconds(100);
        private readonly TimeSpan _timeout = TimeSpan.FromMilliseconds(60_000); // definitely should receive telemetry by now
        private readonly TestTelemetryTransport _transport;
        private readonly TelemetryTransportManager _transportManager;

        public TelemetryControllerTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
            _transport = new TestTelemetryTransport(pushResult: TelemetryPushResult.Success);
            _transportManager = new TelemetryTransportManager(new ITelemetryTransport[] { _transport });
        }

        [Fact]
        public async Task TelemetryControllerShouldSendTelemetry()
        {
            DatadogLogging.SetLogLevel(LogEventLevel.Debug);
            var controller = new TelemetryController(
                new ConfigurationTelemetryCollector(),
                new DependencyTelemetryCollector(),
                new IntegrationTelemetryCollector(),
                _transportManager,
                _refreshInterval,
                _refreshInterval);

            controller.RecordTracerSettings(new ImmutableTracerSettings(new TracerSettings()), "DefaultServiceName");
            controller.Start();
            Log.Debug("Started the controller");

            var data = await WaitForRequestStarted(_transport, _timeout);
            DatadogLogging.SetLogLevel(LogEventLevel.Information);
        }

        [Fact]
        public async Task TelemetryControllerCanBeDisposedTwice()
        {
            var controller = new TelemetryController(
                new ConfigurationTelemetryCollector(),
                new DependencyTelemetryCollector(),
                new IntegrationTelemetryCollector(),
                _transportManager,
                _refreshInterval,
                _refreshInterval);

            await controller.DisposeAsync();
            await controller.DisposeAsync();
        }

        [Fact]
        public async Task TelemetryControllerDisposesOnTwoFatalErrorsFromTelemetry()
        {
            var transport = new TestTelemetryTransport(pushResult: TelemetryPushResult.FatalError); // fail to push telemetry
            var transportManager = new TelemetryTransportManager(new ITelemetryTransport[] { transport });
            var controller = new TelemetryController(
                new ConfigurationTelemetryCollector(),
                new DependencyTelemetryCollector(),
                new IntegrationTelemetryCollector(),
                transportManager,
                _refreshInterval,
                _refreshInterval);

            controller.RecordTracerSettings(new ImmutableTracerSettings(new TracerSettings()), "DefaultServiceName");
            controller.Start();

            (await WaitForFatalError(controller)).Should().BeTrue("controller should be disposed on failed push");

            var previousDataCount = transport.GetData();

            previousDataCount
               .Where(x => x.RequestType != TelemetryRequestTypes.AppHeartbeat)
               .Should()
               .OnlyContain(x => x.RequestType == TelemetryRequestTypes.AppStarted, "Fatal error should mean we try to send app-started twice");

            controller.IntegrationRunning(IntegrationId.Kafka);

            // Shouldn't receive any more data,
            await Task.Delay(3_000);
            transport.GetData().Count.Should().Be(previousDataCount.Count, "Should not send more data after disposal");
        }

        [Fact]
        public async Task TelemetrySendsHeartbeatsIndependentlyOfData()
        {
            var heartBeatInterval = TimeSpan.FromMilliseconds(100);
            var transport = new TestTelemetryTransport(pushResult: TelemetryPushResult.Success);
            var transportManager = new TelemetryTransportManager(new ITelemetryTransport[] { transport });
            var controller = new TelemetryController(
                new ConfigurationTelemetryCollector(),
                new DependencyTelemetryCollector(),
                new IntegrationTelemetryCollector(),
                transportManager,
                flushInterval: TimeSpan.FromMinutes(1),
                heartBeatInterval: heartBeatInterval);

            controller.RecordTracerSettings(new ImmutableTracerSettings(new TracerSettings()), "DefaultServiceName");
            controller.Start();

            var requiredHeartbeats = 10;
            var deadline = DateTimeOffset.UtcNow.AddSeconds(heartBeatInterval.TotalSeconds * 100);
            while (DateTimeOffset.UtcNow < deadline)
            {
                var heartBeatCount = transport.GetData().Count(x => x.RequestType == TelemetryRequestTypes.AppHeartbeat);
                if (heartBeatCount >= requiredHeartbeats)
                {
                    break;
                }

                await Task.Delay(_refreshInterval);
            }

            transport.GetData()
                     .Where(x => x.RequestType == TelemetryRequestTypes.AppHeartbeat)
                     .Should()
                     .HaveCountGreaterOrEqualTo(requiredHeartbeats);
        }

        [Fact]
        public async Task TelemetryControllerAddsAllAssembliesToCollector()
        {
            var currentAssemblyNames = AppDomain.CurrentDomain
                                                .GetAssemblies()
                                                .Where(x => !x.IsDynamic)
                                                .Select(x => x.GetName())
                                                .Select(name => new { name.Name, Version = name.Version.ToString() });

            // creating a new controller so we have the same list of assemblies
            var controller = new TelemetryController(
                new ConfigurationTelemetryCollector(),
                new DependencyTelemetryCollector(),
                new IntegrationTelemetryCollector(),
                _transportManager,
                _refreshInterval,
                _refreshInterval);

            controller.RecordTracerSettings(new ImmutableTracerSettings(new TracerSettings()), "DefaultServiceName");
            controller.Start();

            var allData = await WaitForRequestStarted(_transport, _timeout);
            var payload = allData
                         .Where(x => x.RequestType == TelemetryRequestTypes.AppStarted)
                         .OrderByDescending(x => x.SeqId)
                         .First()
                         .Payload as AppStartedPayload;

            payload.Should().NotBeNull();

            // should contain all the assemblies
            using var a = new AssertionScope();
            foreach (var assemblyName in currentAssemblyNames)
            {
                payload.Dependencies
                       .Should()
                       .ContainEquivalentOf(assemblyName);
            }
        }

        private async Task<List<TelemetryData>> WaitForRequestStarted(TestTelemetryTransport transport, TimeSpan timeout)
        {
            var deadline = DateTimeOffset.UtcNow.Add(timeout);
            while (DateTimeOffset.UtcNow < deadline)
            {
                Log.Debug("Now: " + DateTimeOffset.UtcNow);
                Log.Debug("Deadline: " + deadline);
                var data = transport.GetData();
                Log.Debug("Received data. Nb elements: " + data.Count);

                foreach (var telemetryData in data)
                {
                    Log.Debug("Request type: " + telemetryData.RequestType);
                }

                if (data.Any(x => x.RequestType == TelemetryRequestTypes.AppStarted))
                {
                    return data;
                }

                Log.Debug("Waiting");
                await Task.Delay(_refreshInterval);
            }

            Log.Debug("Giving up");
            DatadogLogging.SetLogLevel(LogEventLevel.Information);

            throw new TimeoutException($"Transport did not receive required data before the timeout {timeout.TotalMilliseconds}ms");
        }

        private async Task<bool> WaitForFatalError(TelemetryController controller)
        {
            var deadline = DateTimeOffset.UtcNow.Add(_timeout);
            while (DateTimeOffset.UtcNow < deadline)
            {
                if (controller.FatalError)
                {
                    // was disposed
                    return true;
                }

                await Task.Delay(_refreshInterval);
            }

            return false;
        }

        internal class TestTelemetryTransport : ITelemetryTransport
        {
            private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<TestTelemetryTransport>();

            private readonly ConcurrentStack<TelemetryData> _data = new();
            private readonly TelemetryPushResult _pushResult;

            public TestTelemetryTransport(TelemetryPushResult pushResult)
            {
                _pushResult = pushResult;
            }

            public List<TelemetryData> GetData()
            {
                return _data.ToList();
            }

            public Task<TelemetryPushResult> PushTelemetry(TelemetryData data)
            {
                Log.Debug("Pushing in tests");
                _data.Push(data);
                return Task.FromResult(_pushResult);
            }

            public string GetTransportInfo() => nameof(TestTelemetryTransport);
        }
    }
}
