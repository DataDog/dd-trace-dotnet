// <copyright file="TelemetryTransportManagerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Telemetry;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Telemetry
{
    public class TelemetryTransportManagerTests
    {
        [Fact]
        public async Task WhenHaveSuccess_ReturnsSuccess()
        {
            const int requestCount = 10;
            var telemetryPushResults = Enumerable.Repeat(TelemetryPushResult.Success, requestCount).ToArray();
            var transportManager = TestTransport.CreateManagerWith(telemetryPushResults);

            var config = Array.Empty<TelemetryValue>();
            var deps = Array.Empty<DependencyTelemetryData>();
            var integrations = Array.Empty<IntegrationTelemetryData>();

            for (var i = 0; i < requestCount; i++)
            {
                var telemetryPushResult = await transportManager.TryPushTelemetry(null!, config, deps, integrations);
                telemetryPushResult.Should().Be(true);
                transportManager.PreviousConfiguration.Should().BeNull();
                transportManager.PreviousDependencies.Should().BeNull();
                transportManager.PreviousIntegrations.Should().BeNull();
            }
        }

        [Theory]
        [InlineData((int)TelemetryPushResult.TransientFailure)]
        [InlineData((int)TelemetryPushResult.FatalError)]
        public async Task OnInitialError_TreatsAsTransientAndSavesDataForLater(int errorType)
        {
            var transportManager = TestTransport.CreateManagerWith((TelemetryPushResult)errorType);

            var config = Array.Empty<TelemetryValue>();
            var deps = Array.Empty<DependencyTelemetryData>();
            var integrations = Array.Empty<IntegrationTelemetryData>();

            var telemetryPushResult = await transportManager.TryPushTelemetry(null!, config, deps, integrations);

            telemetryPushResult.Should().Be(true);
            transportManager.PreviousConfiguration.Should().BeSameAs(config);
            transportManager.PreviousDependencies.Should().BeSameAs(deps);
            transportManager.PreviousIntegrations.Should().BeSameAs(integrations);
        }

        [Fact]
        public async Task OnMultipleInitialFatalError_ReturnsFatal()
        {
            var transportManager = TestTransport.CreateManagerWith(TelemetryPushResult.FatalError, TelemetryPushResult.FatalError);

            var config = Array.Empty<TelemetryValue>();
            var deps = Array.Empty<DependencyTelemetryData>();
            var integrations = Array.Empty<IntegrationTelemetryData>();

            await transportManager.TryPushTelemetry(null!, config, deps, integrations);
            var telemetryPushResult = await transportManager.TryPushTelemetry(null!, config, deps, integrations);

            telemetryPushResult.Should().Be(false);
            transportManager.PreviousConfiguration.Should().BeNull();
            transportManager.PreviousDependencies.Should().BeNull();
            transportManager.PreviousIntegrations.Should().BeNull();
        }

        [Fact]
        public async Task OnMultipleFatalErrorAfterSuccess_ReturnsTransient()
        {
            var transportManager = TestTransport.CreateManagerWith(
                TelemetryPushResult.Success,
                TelemetryPushResult.FatalError,
                TelemetryPushResult.FatalError);

            var config = Array.Empty<TelemetryValue>();
            var deps = Array.Empty<DependencyTelemetryData>();
            var integrations = Array.Empty<IntegrationTelemetryData>();

            await transportManager.TryPushTelemetry(null!, config, deps, integrations);
            var firstResult = await transportManager.TryPushTelemetry(null!, config, deps, integrations);
            var secondResult = await transportManager.TryPushTelemetry(null!, config, deps, integrations);

            firstResult.Should().Be(true);
            secondResult.Should().Be(true);
            transportManager.PreviousConfiguration.Should().BeSameAs(config);
            transportManager.PreviousDependencies.Should().BeSameAs(deps);
            transportManager.PreviousIntegrations.Should().BeSameAs(integrations);
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

            var config = Array.Empty<TelemetryValue>();
            var deps = Array.Empty<DependencyTelemetryData>();
            var integrations = Array.Empty<IntegrationTelemetryData>();

            await transportManager.TryPushTelemetry(null!, config, deps, integrations);
            for (var i = 0; i < TelemetryTransportManager.MaxTransientErrors - 1; i++)
            {
                var result = await transportManager.TryPushTelemetry(null!, config, deps, integrations);
                result.Should().Be(true); // transient
            }

            var finalResult = await transportManager.TryPushTelemetry(null!, config, deps, integrations);

            finalResult.Should().Be(false);
            transportManager.PreviousConfiguration.Should().BeNull();
            transportManager.PreviousDependencies.Should().BeNull();
            transportManager.PreviousIntegrations.Should().BeNull();
        }

        [Fact]
        public async Task OnSuccessAfterTransient_ClearsPreviousConfig()
        {
            var transportManager = TestTransport.CreateManagerWith(TelemetryPushResult.TransientFailure, TelemetryPushResult.Success);

            var config = Array.Empty<TelemetryValue>();
            var deps = Array.Empty<DependencyTelemetryData>();
            var integrations = Array.Empty<IntegrationTelemetryData>();

            await transportManager.TryPushTelemetry(null!, config, deps, integrations);
            await transportManager.TryPushTelemetry(null!, config, deps, integrations);

            transportManager.PreviousConfiguration.Should().BeNull();
            transportManager.PreviousDependencies.Should().BeNull();
            transportManager.PreviousIntegrations.Should().BeNull();
        }

        [Fact]
        public async Task WithASingleTransport_FatalErrorDontRetryImmediatelyFails()
        {
            var transportManager = TestTransport.CreateManagerWith(TelemetryPushResult.FatalErrorDontRetry);

            var config = Array.Empty<TelemetryValue>();
            var deps = Array.Empty<DependencyTelemetryData>();
            var integrations = Array.Empty<IntegrationTelemetryData>();

            var result = await transportManager.TryPushTelemetry(null!, config, deps, integrations);

            result.Should().BeFalse();
        }

        [Fact]
        public async Task WithTwoTransports_FatalErrorDontRetryTriggersUsingSecondTransport()
        {
            var transport1 = new TestTransport(TelemetryPushResult.FatalErrorDontRetry);
            var transport2 = new TestTransport(TelemetryPushResult.Success, TelemetryPushResult.FatalErrorDontRetry);
            var transportManager = new TelemetryTransportManager(new ITelemetryTransport[] { transport1, transport2 });

            var config = Array.Empty<TelemetryValue>();
            var deps = Array.Empty<DependencyTelemetryData>();
            var integrations = Array.Empty<IntegrationTelemetryData>();

            var result = await transportManager.TryPushTelemetry(null!, config, deps, integrations);
            result.Should().BeTrue(); // keep sending traces, should use second transport

            result = await transportManager.TryPushTelemetry(null!, config, deps, integrations);
            result.Should().BeTrue(); // using second transport now

            result = await transportManager.TryPushTelemetry(null!, config, deps, integrations);
            result.Should().BeFalse(); // hit second fatal error
        }

        [Fact]
        public async Task WithTwoTransports_RepeatedInitialFatalErrors_TriggersUsingSecondTransport()
        {
            var transport1 = new TestTransport(TelemetryPushResult.FatalError, TelemetryPushResult.FatalError);
            var transport2 = new TestTransport(TelemetryPushResult.Success, TelemetryPushResult.FatalErrorDontRetry);
            var transportManager = new TelemetryTransportManager(new ITelemetryTransport[] { transport1, transport2 });

            var config = Array.Empty<TelemetryValue>();
            var deps = Array.Empty<DependencyTelemetryData>();
            var integrations = Array.Empty<IntegrationTelemetryData>();

            await transportManager.TryPushTelemetry(null!, config, deps, integrations);
            var result = await transportManager.TryPushTelemetry(null!, config, deps, integrations);
            result.Should().BeTrue(); // keep sending traces, should use second transport

            result = await transportManager.TryPushTelemetry(null!, config, deps, integrations);
            result.Should().BeTrue(); // using second transport now

            result = await transportManager.TryPushTelemetry(null!, config, deps, integrations);
            result.Should().BeFalse(); // hit second fatal error
        }

        [Fact]
        public async Task WithOneTransport_WhenRunOutOfTransports_ImmediatelyStopsWithoutCallingTransport()
        {
            var transportManager = TestTransport.CreateManagerWith(TelemetryPushResult.FatalErrorDontRetry);

            var config = Array.Empty<TelemetryValue>();
            var deps = Array.Empty<DependencyTelemetryData>();
            var integrations = Array.Empty<IntegrationTelemetryData>();

            var result = await transportManager.TryPushTelemetry(null!, config, deps, integrations);
            result.Should().BeFalse(); // fatal error

            // shouldn't call this, but make sure we don't crash
            result = await transportManager.TryPushTelemetry(null!, config, deps, integrations);
            result.Should().BeFalse(); // short circuit
        }

        [Fact]
        public async Task WithTwoTransports_WhenRunOutOfTransports_ImmediatelyStopsWithoutCallingTransport()
        {
            var transport1 = new TestTransport(TelemetryPushResult.FatalErrorDontRetry);
            var transport2 = new TestTransport(TelemetryPushResult.FatalErrorDontRetry);
            var transportManager = new TelemetryTransportManager(new ITelemetryTransport[] { transport1, transport2 });

            var config = Array.Empty<TelemetryValue>();
            var deps = Array.Empty<DependencyTelemetryData>();
            var integrations = Array.Empty<IntegrationTelemetryData>();

            var result = await transportManager.TryPushTelemetry(null!, config, deps, integrations);
            result.Should().BeTrue(); // fatal error, on to next transport

            result = await transportManager.TryPushTelemetry(null!, config, deps, integrations);
            result.Should().BeFalse(); // fatal error

            // shouldn't call this, but make sure we don't crash
            result = await transportManager.TryPushTelemetry(null!, config, deps, integrations);
            result.Should().BeFalse();
        }

        [Fact]
        public async Task TestTransport_ThrowsIfCalledTwoManyTimes()
        {
            var transport1 = new TestTransport(TelemetryPushResult.TransientFailure);
            var transportManager = new TelemetryTransportManager(new ITelemetryTransport[] { transport1 });

            var config = Array.Empty<TelemetryValue>();
            var deps = Array.Empty<DependencyTelemetryData>();
            var integrations = Array.Empty<IntegrationTelemetryData>();

            await transportManager.TryPushTelemetry(null!, config, deps, integrations);

            var call = async () => await transportManager.TryPushTelemetry(null!, config, deps, integrations);
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

            public static TelemetryTransportManager CreateManagerWith(params TelemetryPushResult[] results)
            {
                return new TelemetryTransportManager(new ITelemetryTransport[] { new TestTransport(results) });
            }

            public Task<TelemetryPushResult> PushTelemetry(TelemetryData data)
            {
                _current++;
                if (_current >= _results.Length)
                {
                    throw new InvalidOperationException("Transport received unexpected request");
                }

                return Task.FromResult(_results[_current]);
            }
        }
    }
}
