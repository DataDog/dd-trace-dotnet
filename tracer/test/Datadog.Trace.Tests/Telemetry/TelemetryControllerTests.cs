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
using Datadog.Trace.Telemetry;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;

namespace Datadog.Trace.Tests.Telemetry
{
    public class TelemetryControllerTests
    {
        private readonly TimeSpan _refreshInterval = TimeSpan.FromMilliseconds(100);
        private readonly TimeSpan _heartbeatInterval = TimeSpan.FromMilliseconds(10_000); // We don't need them for most tests
        private readonly TimeSpan _timeout = TimeSpan.FromMilliseconds(60_000); // definitely should receive telemetry by now

        [Fact]
        public async Task TelemetryControllerShouldSendTelemetry()
        {
            var transport = new TestTelemetryTransport(pushResult: TelemetryPushResult.Success);
            var transportManager = new TelemetryTransportManager(new ITelemetryTransport[] { transport });

            var controller = new TelemetryController(
                new ConfigurationTelemetryCollector(),
                new DependencyTelemetryCollector(),
                new IntegrationTelemetryCollector(),
                transportManager,
                _refreshInterval,
                _heartbeatInterval);

            controller.RecordTracerSettings(new ImmutableTracerSettings(new TracerSettings()), "DefaultServiceName");
            controller.Start();

            var data = await WaitForRequestStarted(transport, _timeout);
            await controller.DisposeAsync(false);
        }

        [Fact]
        public async Task TelemetryControllerCanBeDisposedTwice()
        {
            var transport = new TestTelemetryTransport(pushResult: TelemetryPushResult.Success);
            var transportManager = new TelemetryTransportManager(new ITelemetryTransport[] { transport });
            var controller = new TelemetryController(
                new ConfigurationTelemetryCollector(),
                new DependencyTelemetryCollector(),
                new IntegrationTelemetryCollector(),
                transportManager,
                _refreshInterval,
                _heartbeatInterval);

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
                _heartbeatInterval);

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
            await controller.DisposeAsync(false);
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

            await controller.DisposeAsync(false);
        }

        [Fact]
        public async Task TelemetryControllerAddsAllAssembliesToCollector()
        {
            var transport = new TestTelemetryTransport(pushResult: TelemetryPushResult.Success);
            var transportManager = new TelemetryTransportManager(new ITelemetryTransport[] { transport });

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
                transportManager,
                _refreshInterval,
                _heartbeatInterval);

            controller.RecordTracerSettings(new ImmutableTracerSettings(new TracerSettings()), "DefaultServiceName");
            controller.Start();

            var allData = await WaitForRequestStarted(transport, _timeout);
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

            await controller.DisposeAsync(false);
        }

        private async Task<List<TelemetryData>> WaitForRequestStarted(TestTelemetryTransport transport, TimeSpan timeout)
        {
            var deadline = DateTimeOffset.UtcNow.Add(timeout);
            // The Task.Delay happens to give back control after the deadline so the test can fail randomly
            // So I add a notion of number of tries
            var nbTries = 0;
            while (DateTimeOffset.UtcNow < deadline || nbTries < 3)
            {
                nbTries++;
                var data = transport.GetData();

                if (data.Any(x => x.RequestType == TelemetryRequestTypes.AppStarted))
                {
                    return data;
                }

                await Task.Delay(_refreshInterval);
            }

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
                _data.Push(data);
                return Task.FromResult(_pushResult);
            }

            public string GetTransportInfo() => nameof(TestTelemetryTransport);
        }
    }
}
