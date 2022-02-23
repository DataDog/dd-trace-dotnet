// <copyright file="IntegrationRegistryTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Configuration;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;

namespace Datadog.Trace.Tests.Configuration
{
    public class IntegrationRegistryTests
    {
        [Fact]
        public void CanRoundTripIntegrationIds()
        {
            using var scope = new AssertionScope();
            var values = IntegrationRegistry.Ids.Values;
            values.Should().HaveCountGreaterThan(0);

            foreach (var i in IntegrationRegistry.Ids.Values)
            {
                var integrationId = (IntegrationId)i;
                var name = IntegrationRegistry.GetName(integrationId);
                IntegrationRegistry.TryGetIntegrationId(name, out var parsedId1).Should().BeTrue();
                IntegrationRegistry.TryGetIntegrationId(name.ToUpperInvariant(), out var parsedId2).Should().BeTrue();
                IntegrationRegistry.TryGetIntegrationId(name.ToLowerInvariant(), out var parsedId3).Should().BeTrue();

                parsedId1.Should().Be(integrationId);
                parsedId2.Should().Be(integrationId);
                parsedId3.Should().Be(integrationId);
            }
        }

        [Fact]
        public void TryGetIntegrationId_ReturnsFalseForUnknownIntegration()
        {
            IntegrationRegistry.TryGetIntegrationId("blobby", out _).Should().BeFalse();
        }
    }
}
