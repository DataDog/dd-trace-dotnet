// <copyright file="TelemetryCircuitBreakerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Telemetry;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Telemetry
{
    public class TelemetryCircuitBreakerTests
    {
        private readonly TelemetryCircuitBreaker _circuitBreaker = new();

        [Fact]
        public void WhenHaveSuccess_ReturnsSuccess()
        {
            var result = TelemetryPushResult.Success;
            var config = new ConfigTelemetryData();
            var deps = Array.Empty<DependencyTelemetryData>();
            var integrations = Array.Empty<IntegrationTelemetryData>();

            for (var i = 0; i < 10; i++)
            {
                var telemetryPushResult = _circuitBreaker.Evaluate(result, config, deps, integrations);
                telemetryPushResult.Should().Be(TelemetryPushResult.Success);
                _circuitBreaker.PreviousConfiguration.Should().BeNull();
                _circuitBreaker.PreviousDependencies.Should().BeNull();
                _circuitBreaker.PreviousIntegrations.Should().BeNull();
            }
        }

        [Theory]
        [InlineData((int)TelemetryPushResult.TransientFailure)]
        [InlineData((int)TelemetryPushResult.FatalError)]
        public void OnInitialError_TreatsAsTransientAndSavesDataForLater(int errorType)
        {
            var config = new ConfigTelemetryData();
            var deps = Array.Empty<DependencyTelemetryData>();
            var integrations = Array.Empty<IntegrationTelemetryData>();

            var telemetryPushResult = _circuitBreaker.Evaluate((TelemetryPushResult)errorType, config, deps, integrations);

            telemetryPushResult.Should().Be(TelemetryPushResult.TransientFailure);
            _circuitBreaker.PreviousConfiguration.Should().BeSameAs(config);
            _circuitBreaker.PreviousDependencies.Should().BeSameAs(deps);
            _circuitBreaker.PreviousIntegrations.Should().BeSameAs(integrations);
        }

        [Fact]
        public void OnMultipleInitialFatalError_ReturnsFatal()
        {
            var config = new ConfigTelemetryData();
            var deps = Array.Empty<DependencyTelemetryData>();
            var integrations = Array.Empty<IntegrationTelemetryData>();

            _circuitBreaker.Evaluate(TelemetryPushResult.FatalError, config, deps, integrations);
            var telemetryPushResult = _circuitBreaker.Evaluate(TelemetryPushResult.FatalError, config, deps, integrations);

            telemetryPushResult.Should().Be(TelemetryPushResult.FatalError);
            _circuitBreaker.PreviousConfiguration.Should().BeNull();
            _circuitBreaker.PreviousDependencies.Should().BeNull();
            _circuitBreaker.PreviousIntegrations.Should().BeNull();
        }

        [Fact]
        public void OnMultipleFatalErrorAfterSuccess_ReturnsTransient()
        {
            var config = new ConfigTelemetryData();
            var deps = Array.Empty<DependencyTelemetryData>();
            var integrations = Array.Empty<IntegrationTelemetryData>();

            _circuitBreaker.Evaluate(TelemetryPushResult.Success, config, deps, integrations);
            var firstResult = _circuitBreaker.Evaluate(TelemetryPushResult.FatalError, config, deps, integrations);
            var secondResult = _circuitBreaker.Evaluate(TelemetryPushResult.FatalError, config, deps, integrations);

            firstResult.Should().Be(TelemetryPushResult.TransientFailure);
            secondResult.Should().Be(TelemetryPushResult.TransientFailure);
        }

        [Fact]
        public void OnMultipleTransientErrorsAfterSuccess_ReturnsFatal()
        {
            var config = new ConfigTelemetryData();
            var deps = Array.Empty<DependencyTelemetryData>();
            var integrations = Array.Empty<IntegrationTelemetryData>();

            _circuitBreaker.Evaluate(TelemetryPushResult.Success, config, deps, integrations);
            for (var i = 0; i < TelemetryCircuitBreaker.MaxTransientErrors - 1; i++)
            {
                var result = _circuitBreaker.Evaluate(TelemetryPushResult.FatalError, config, deps, integrations);
                result.Should().Be(TelemetryPushResult.TransientFailure);
            }

            var finalResult = _circuitBreaker.Evaluate(TelemetryPushResult.FatalError, config, deps, integrations);

            finalResult.Should().Be(TelemetryPushResult.FatalError);
            _circuitBreaker.PreviousConfiguration.Should().BeNull();
            _circuitBreaker.PreviousDependencies.Should().BeNull();
            _circuitBreaker.PreviousIntegrations.Should().BeNull();
        }

        [Fact]
        public void OnSuccessAfterTransient_ClearsPreviousConfig()
        {
            var config = new ConfigTelemetryData();
            var deps = Array.Empty<DependencyTelemetryData>();
            var integrations = Array.Empty<IntegrationTelemetryData>();

            _circuitBreaker.Evaluate(TelemetryPushResult.TransientFailure, config, deps, integrations);
            _circuitBreaker.Evaluate(TelemetryPushResult.Success, config, deps, integrations);

            _circuitBreaker.PreviousConfiguration.Should().BeNull();
            _circuitBreaker.PreviousDependencies.Should().BeNull();
            _circuitBreaker.PreviousIntegrations.Should().BeNull();
        }
    }
}
