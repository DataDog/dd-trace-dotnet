// <copyright file="TelemetryControllerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.AppSec;
using Datadog.Trace.Configuration;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.Telemetry;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;

namespace Datadog.Trace.Tests.Telemetry
{
    public class TelemetryControllerTests : IDisposable
    {
        private static readonly AzureAppServices EmptyAasMetadata = new(new Dictionary<string, string>());
        private readonly TimeSpan _refreshInterval = TimeSpan.FromMilliseconds(100);
        private readonly TimeSpan _timeout = TimeSpan.FromMilliseconds(10_000); // definitely should receive telemetry by now
        private readonly TestTelemetryTransport _transport;
        private readonly TelemetryController _controller;

        public TelemetryControllerTests()
        {
            _transport = new TestTelemetryTransport(pushResult: true);
            _controller = new TelemetryController(
                new ConfigurationTelemetryCollector(),
                new DependencyTelemetryCollector(),
                new IntegrationTelemetryCollector(),
                _transport,
                _refreshInterval);
        }

        public void Dispose() => _controller?.Dispose();

        [Fact]
        public async Task TelemetryControllerShouldSendTelemetry()
        {
            _controller.RecordTracerSettings(new ImmutableTracerSettings(new TracerSettings()), "DefaultServiceName", EmptyAasMetadata);

            var data = await WaitForRequestStarted(_transport, _timeout);
        }

        [Fact]
        public void TelemetryControllerCanBeDisposedTwice()
        {
            _controller.Dispose();
            _controller.Dispose();
        }

        [Fact]
        public async Task TelemetryControllerDisposesOnFatalErrorFromTelemetry()
        {
            var transport = new TestTelemetryTransport(pushResult: false); // fail to push telemetry
            var controller = new TelemetryController(
                new ConfigurationTelemetryCollector(),
                new DependencyTelemetryCollector(),
                new IntegrationTelemetryCollector(),
                transport,
                _refreshInterval);

            controller.RecordTracerSettings(new ImmutableTracerSettings(new TracerSettings()), "DefaultServiceName", EmptyAasMetadata);

            var data = await WaitForRequestStarted(transport, _timeout);

            (await WaitForFatalError(controller)).Should().BeTrue("controller should be disposed on failed push");

            var previousDataCount = data.Count;

            controller.IntegrationRunning(IntegrationId.Kafka);

            // Shouldn't receive any more data,
            await Task.Delay(3_000);
            transport.GetData().Count.Should().Be(previousDataCount, "Should not send more data after disposal");
        }

        [Fact]
        public async Task TelemetryControllerAddsAllAssembliesToCollector()
        {
            var currentAssemblyNames = AppDomain.CurrentDomain
                                                .GetAssemblies()
                                                .Select(x => x.GetName());

            _controller.RecordTracerSettings(new ImmutableTracerSettings(new TracerSettings()), "DefaultServiceName", EmptyAasMetadata);

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
                    .ContainSingle(x => x.Name == assemblyName.Name && x.Version == assemblyName.Version.ToString());
            }
        }

        private async Task<List<TelemetryData>> WaitForRequestStarted(TestTelemetryTransport transport, TimeSpan timeout)
        {
            var deadline = DateTimeOffset.UtcNow.Add(timeout);
            while (DateTimeOffset.UtcNow < deadline)
            {
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
            private readonly bool _pushResult;

            public TestTelemetryTransport(bool pushResult)
            {
                _pushResult = pushResult;
            }

            public List<TelemetryData> GetData()
            {
                return _data.ToList();
            }

            public Task<bool> PushTelemetry(TelemetryData data)
            {
                _data.Push(data);
                return Task.FromResult(_pushResult);
            }
        }
    }
}
