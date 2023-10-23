// <copyright file="TelemetryControllerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Collectors;
using Datadog.Trace.Telemetry.Transports;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;

namespace Datadog.Trace.Tests.Telemetry;

public class TelemetryControllerTests
{
    private readonly TimeSpan _flushInterval = TimeSpan.FromMilliseconds(100);
    // private readonly TimeSpan _heartbeatInterval = TimeSpan.FromMilliseconds(10_000); // We don't need them for most tests
    private readonly TimeSpan _timeout = TimeSpan.FromMilliseconds(60_000); // definitely should receive telemetry by now

    [Fact]
    public async Task TelemetryControllerShouldSendTelemetry()
    {
        var transport = new TestTelemetryTransport(pushResult: TelemetryPushResult.Success);
        var transportManager = new TelemetryTransportManager(new TelemetryTransports(transport, null), NullDiscoveryService.Instance);

        var controller = new TelemetryController(
            new ConfigurationTelemetry(),
            new DependencyTelemetryCollector(),
            new NullMetricsTelemetryCollector(),
            transportManager,
            _flushInterval);

        controller.RecordTracerSettings(new ImmutableTracerSettings(new TracerSettings()), "DefaultServiceName");
        controller.Start();

        var data = await WaitForRequestStarted(transport, _timeout);
        await controller.DisposeAsync();
    }

    [Fact]
    public async Task TelemetryControllerRecordsConfigurationFromTracerSettings()
    {
        var transport = new TestTelemetryTransport(pushResult: TelemetryPushResult.Success);
        var transportManager = new TelemetryTransportManager(new TelemetryTransports(transport, null), NullDiscoveryService.Instance);

        var collector = new ConfigurationTelemetry();
        var controller = new TelemetryController(
            collector,
            new DependencyTelemetryCollector(),
            new NullMetricsTelemetryCollector(),
            transportManager,
            _flushInterval);

        var settings = new ImmutableTracerSettings(new TracerSettings());
        controller.RecordTracerSettings(settings, "DefaultServiceName");

        // Just basic check that we have the same number of config values
        var configCount = settings.Telemetry.Should()
                                  .BeOfType<ConfigurationTelemetry>()
                                  .Which
                                  .GetQueueForTesting()
                                  .Count
                                  .Should()
                                  .NotBe(0)
                                  .And.Subject;

        collector.GetQueueForTesting().Count.Should().Be(configCount);
        await controller.DisposeAsync();
    }

    [Fact]
    public async Task TelemetryControllerCanBeDisposedTwice()
    {
        var transport = new TestTelemetryTransport(pushResult: TelemetryPushResult.Success);
        var transportManager = new TelemetryTransportManager(new TelemetryTransports(transport, null), NullDiscoveryService.Instance);

        var controller = new TelemetryController(
            new ConfigurationTelemetry(),
            new DependencyTelemetryCollector(),
            new NullMetricsTelemetryCollector(),
            transportManager,
            _flushInterval);

        await controller.DisposeAsync();
        await controller.DisposeAsync();
    }

    [Fact]
    public async Task TelemetrySendsHeartbeatAlongWithData()
    {
        var transport = new TestTelemetryTransport(pushResult: TelemetryPushResult.Success);
        var transportManager = new TelemetryTransportManager(new TelemetryTransports(transport, null), NullDiscoveryService.Instance);

        var controller = new TelemetryController(
            new ConfigurationTelemetry(),
            new DependencyTelemetryCollector(),
            new NullMetricsTelemetryCollector(),
            transportManager,
            _flushInterval);

        controller.RecordTracerSettings(new ImmutableTracerSettings(new TracerSettings()), "DefaultServiceName");
        controller.Start();

        var requiredHeartbeats = 10;
        var deadline = DateTimeOffset.UtcNow.AddSeconds(_flushInterval.TotalSeconds * 1000);
        while (DateTimeOffset.UtcNow < deadline)
        {
            var heartBeatCount = transport.GetData().Count(x => ContainsMessage(x, TelemetryRequestTypes.AppHeartbeat));
            if (heartBeatCount >= requiredHeartbeats)
            {
                break;
            }

            await Task.Delay(_flushInterval);
        }

        transport.GetData()
                 .Where(x => ContainsMessage(x, TelemetryRequestTypes.AppHeartbeat))
                 .Should()
                 .HaveCountGreaterOrEqualTo(requiredHeartbeats);

        await controller.DisposeAsync();
    }

    [Fact]
    public async Task TelemetryControllerAddsAllAssembliesToCollector()
    {
        var transport = new TestTelemetryTransport(pushResult: TelemetryPushResult.Success);
        var transportManager = new TelemetryTransportManager(new TelemetryTransports(transport, null), NullDiscoveryService.Instance);

        var currentAssemblyNames = AppDomain.CurrentDomain
                                            .GetAssemblies()
                                            .Where(x => !x.IsDynamic)
                                            .Select(x => x.GetName())
                                            .Select(name => new { name.Name, Version = name.Version.ToString() });

        // creating a new controller so we have the same list of assemblies
        var controller = new TelemetryController(
            new ConfigurationTelemetry(),
            new DependencyTelemetryCollector(),
            new NullMetricsTelemetryCollector(),
            transportManager,
            _flushInterval);

        controller.RecordTracerSettings(new ImmutableTracerSettings(new TracerSettings()), "DefaultServiceName");
        controller.Start();

        var allData = await WaitForRequestStarted(transport, _timeout);
        var dependencies = allData
                          .Where(x => ContainsMessage(x, TelemetryRequestTypes.AppDependenciesLoaded))
                          .Select(x => GetPayload(x, TelemetryRequestTypes.AppDependenciesLoaded).Payload)
                          .Cast<AppDependenciesLoadedPayload>()
                          .SelectMany(x => x.Dependencies)
                          .ToList();

        // should contain all the assemblies
        using var a = new AssertionScope();
        foreach (var assemblyName in currentAssemblyNames)
        {
            dependencies
               .Should()
               .Contain(
                    x => x.Name.Equals(assemblyName.Name, StringComparison.OrdinalIgnoreCase)
                      && x.Version.Equals(assemblyName.Version, StringComparison.OrdinalIgnoreCase));
        }

        await controller.DisposeAsync();
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

            if (data.Any(x => ContainsMessage(x, TelemetryRequestTypes.AppStarted)))
            {
                return data;
            }

            await Task.Delay(_flushInterval);
        }

        throw new TimeoutException($"Transport did not receive required data before the timeout {timeout.TotalMilliseconds}ms");
    }

    private bool ContainsMessage(TelemetryData data, string requestType)
        => GetPayload(data, requestType).Found;

    private (bool Found, IPayload Payload) GetPayload(TelemetryData data, string requestType) =>
        data.RequestType switch
        {
            { } t when t == requestType => (true, data.Payload),
            TelemetryRequestTypes.MessageBatch => data.Payload switch
            {
                MessageBatchPayload batch => batch
                                            .FirstOrDefault(p => p.RequestType == requestType)
                                                 is { } msg
                                                 ? (true, msg.Payload)
                                                 : (false, null),
                _ => (false, null),
            },
            _ => (false, null),
        };

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

    internal class SlowTelemetryTransport : ITelemetryTransport
    {
        private readonly TimeSpan _delay;
        private int _requests = 0;

        public SlowTelemetryTransport(TimeSpan delay)
        {
            _delay = delay;
        }

        public int Requests => Volatile.Read(ref _requests);

        public async Task<TelemetryPushResult> PushTelemetry(TelemetryData data)
        {
            Interlocked.Increment(ref _requests);
            await Task.Delay(_delay);
            return TelemetryPushResult.Success;
        }

        public string GetTransportInfo() => nameof(SlowTelemetryTransport);
    }
}
