// <copyright file="IntegrationSettingsCollectionTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Configuration;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Configuration
{
    public class IntegrationSettingsCollectionTests
    {
        [Fact]
        public void ReturnsIntegrationWhenUsingIncorrectCasing()
        {
            var settings = new IntegrationSettingsCollection(null);

            var log4NetByName = settings["LOG4NET"];
            var log4NetById = settings[nameof(IntegrationId.Log4Net)];

            log4NetById.Should().Be(log4NetByName);
        }

        [Fact]
        public void ReturnsDefaultSettingsForUnknownIntegration()
        {
            var settings = new IntegrationSettingsCollection(null);

            var integrationName = "blobby";
            var instance1 = settings[integrationName];

            instance1.IntegrationName.Should().Be(integrationName);
            instance1.Enabled.Should().BeNull();

            instance1.Enabled = true;

            var instance2 = settings[integrationName];
            instance2.Should().NotBe(instance1);
            instance2.IntegrationName.Should().Be(integrationName);
            instance2.Enabled.Should().BeNull();
        }
    }
}
