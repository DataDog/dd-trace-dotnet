// <copyright file="TelemetryTransportManagerV2Tests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Telemetry;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Telemetry;

public class TelemetryTransportManagerV2Tests
{
    [Fact]
    public async Task WhenHaveSuccess_ReturnsSuccess()
    {
        const int requestCount = 10;
        var telemetryPushResults = Enumerable.Repeat(TelemetryPushResult.Success, requestCount).ToArray();
        var transportManager = TestTransport.CreateManagerWith(telemetryPushResults);

        for (var i = 0; i < requestCount; i++)
        {
            var telemetryPushResult = await transportManager.TryPushTelemetry(null!);
            telemetryPushResult.Should().Be(TelemetryTransportResult.Success);
        }
    }

    [Theory]
    [InlineData((int)TelemetryPushResult.TransientFailure)]
    [InlineData((int)TelemetryPushResult.FatalError)]
    public async Task OnInitialError_TreatsAsTransient(int errorType)
    {
        var transportManager = TestTransport.CreateManagerWith((TelemetryPushResult)errorType);

        var telemetryPushResult = await transportManager.TryPushTelemetry(null!);

        telemetryPushResult.Should().Be(TelemetryTransportResult.TransientError);
    }

    [Fact]
    public async Task OnMultipleInitialFatalError_ReturnsFatal()
    {
        var transportManager = TestTransport.CreateManagerWith(TelemetryPushResult.FatalError, TelemetryPushResult.FatalError);

        await transportManager.TryPushTelemetry(null!);
        var telemetryPushResult = await transportManager.TryPushTelemetry(null!);

        telemetryPushResult.Should().Be(TelemetryTransportResult.FatalError);
    }

    [Fact]
    public async Task OnMultipleFatalErrorAfterSuccess_ReturnsTransient()
    {
        var transportManager = TestTransport.CreateManagerWith(
            TelemetryPushResult.Success,
            TelemetryPushResult.FatalError,
            TelemetryPushResult.FatalError);

        await transportManager.TryPushTelemetry(null!);
        var firstResult = await transportManager.TryPushTelemetry(null!);
        var secondResult = await transportManager.TryPushTelemetry(null!);

        firstResult.Should().Be(TelemetryTransportResult.TransientError);
        secondResult.Should().Be(TelemetryTransportResult.TransientError);
    }

    [Theory]
    [InlineData((int)TelemetryPushResult.FatalError)]
    [InlineData((int)TelemetryPushResult.TransientFailure)]
    public async Task OnMultipleTransientErrorsAfterSuccess_ReturnsFatal(int errorType)
    {
        var results = new[] { TelemetryPushResult.Success }
                     .Concat(Enumerable.Repeat((TelemetryPushResult)errorType, TelemetryTransportManager.MaxTransientErrors))
                     .Concat(new[] { TelemetryPushResult.FatalError })
                     .ToArray();

        var transportManager = TestTransport.CreateManagerWith(results);

        await transportManager.TryPushTelemetry(null!);
        for (var i = 0; i < TelemetryTransportManager.MaxTransientErrors - 1; i++)
        {
            var result = await transportManager.TryPushTelemetry(null!);
            result.Should().Be(TelemetryTransportResult.TransientError);
        }

        var finalResult = await transportManager.TryPushTelemetry(null!);

        finalResult.Should().Be(TelemetryTransportResult.FatalError);
    }

    [Fact]
    public async Task WithOneTransport_WhenRunOutOfTransports_ImmediatelyStopsWithoutCallingTransport()
    {
        var transportManager = TestTransport.CreateManagerWith(TelemetryPushResult.FatalError, TelemetryPushResult.FatalError);

        await transportManager.TryPushTelemetry(null!);
        var result = await transportManager.TryPushTelemetry(null!);
        result.Should().Be(TelemetryTransportResult.FatalError);

        // This shouldn't be called by parent, but makes sure we don't crash
        result = await transportManager.TryPushTelemetry(null!);
        result.Should().Be(TelemetryTransportResult.FatalError); // short circuit
    }

    [Fact]
    public async Task WithTwoTransports_WhenRunOutOfTransports_ImmediatelyStopsWithoutCallingTransport()
    {
        var transport1 = new TestTransport(TelemetryPushResult.FatalError, TelemetryPushResult.FatalError);
        var transport2 = new TestTransport(TelemetryPushResult.FatalError, TelemetryPushResult.FatalError);
        var transportManager = new TelemetryTransportManagerV2(new ITelemetryTransport[] { transport1, transport2 });

        await transportManager.TryPushTelemetry(null!);
        var result = await transportManager.TryPushTelemetry(null!);
        result.Should().Be(TelemetryTransportResult.TransientError); // fatal error, on to next transport

        await transportManager.TryPushTelemetry(null!);
        result.Should().Be(TelemetryTransportResult.TransientError); // first failure of second transport

        result = await transportManager.TryPushTelemetry(null!);
        result.Should().Be(TelemetryTransportResult.FatalError); // second failure, out of transports

        // shouldn't call this, but make sure we don't crash
        result = await transportManager.TryPushTelemetry(null!);
        result.Should().Be(TelemetryTransportResult.FatalError);
    }

    [Fact]
    public async Task TestTransport_ThrowsIfCalledTooManyTimes()
    {
        var transport1 = new TestTransport(TelemetryPushResult.TransientFailure);
        var transportManager = new TelemetryTransportManagerV2(new ITelemetryTransport[] { transport1 });

        await transportManager.TryPushTelemetry(null!);

        var call = async () => await transportManager.TryPushTelemetry(null!);
        await call.Should().ThrowExactlyAsync<InvalidOperationException>();
    }

    internal class TestTransport : ITelemetryTransport
    {
        private readonly TelemetryPushResult[] _results;
        private int _current = -1;

        public TestTransport(params TelemetryPushResult[] results)
        {
            _results = results;
        }

        public static TelemetryTransportManagerV2 CreateManagerWith(params TelemetryPushResult[] results)
        {
            return new TelemetryTransportManagerV2(new ITelemetryTransport[] { new TestTransport(results) });
        }

        public async Task<TelemetryPushResult> PushTelemetry(TelemetryData data)
        {
            await Task.Yield();
            throw new InvalidOperationException("Shouldn't be using v1 API");
        }

        public Task<TelemetryPushResult> PushTelemetry(TelemetryDataV2 data)
        {
            _current++;
            if (_current >= _results.Length)
            {
                throw new InvalidOperationException("Transport received unexpected request");
            }

            return Task.FromResult(_results[_current]);
        }

        public string GetTransportInfo() => nameof(TestTransport);
    }
}
