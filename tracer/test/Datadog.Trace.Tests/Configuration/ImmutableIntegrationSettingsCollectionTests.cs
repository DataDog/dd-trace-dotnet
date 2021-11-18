// <copyright file="ImmutableIntegrationSettingsCollectionTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Collections.Specialized;
using Datadog.Trace.Configuration;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Configuration
{
    public class ImmutableIntegrationSettingsCollectionTests
    {
        [Fact]
        public void PopulatesFromBuilderCorrectly()
        {
            var source = new NameValueConfigurationSource(new NameValueCollection
            {
                { "DD_TRACE_FOO_ENABLED", "true" },
                { "DD_TRACE_FOO_ANALYTICS_ENABLED", "true" },
                { "DD_TRACE_FOO_ANALYTICS_SAMPLE_RATE", "0.2" },
                { "DD_TRACE_BAR_ENABLED", "false" },
                { "DD_TRACE_BAR_ANALYTICS_ENABLED", "false" },
                { "DD_BAZ_ENABLED", "false" },
                { "DD_BAZ_ANALYTICS_ENABLED", "false" },
                { "DD_BAZ_ANALYTICS_SAMPLE_RATE", "0.6" },
                { "DD_TRACE_Kafka_ENABLED", "true" },
                { "DD_TRACE_Kafka_ANALYTICS_ENABLED", "true" },
                { "DD_TRACE_Kafka_ANALYTICS_SAMPLE_RATE", "0.2" },
                { "DD_TRACE_GraphQL_ENABLED", "false" },
                { "DD_TRACE_GraphQL_ANALYTICS_ENABLED", "false" },
                { "DD_Wcf_ENABLED", "false" },
                { "DD_Wcf_ANALYTICS_ENABLED", "false" },
                { "DD_Wcf_ANALYTICS_SAMPLE_RATE", "0.2" },
                { "DD_Msmq_ENABLED", "true" },
            });

            var disabledIntegrations = new HashSet<string> { "foobar", "MongoDb", "Msmq" };

            var builderCollection = new IntegrationSettingsCollection(source);

            var final = new ImmutableIntegrationSettingsCollection(builderCollection, disabledIntegrations);

            var kafka = final[IntegrationId.Kafka];
            kafka.Enabled.Should().BeTrue();
            kafka.AnalyticsEnabled.Should().BeTrue();
            kafka.AnalyticsSampleRate.Should().Be(0.2);

            var graphql = final[IntegrationId.GraphQL];
            graphql.Enabled.Should().BeFalse();
            graphql.AnalyticsEnabled.Should().BeFalse();
            graphql.AnalyticsSampleRate.Should().Be(1.0);

            var wcf = final[IntegrationId.Wcf];
            wcf.Enabled.Should().BeFalse();
            wcf.AnalyticsEnabled.Should().BeFalse();
            wcf.AnalyticsSampleRate.Should().Be(0.2);

            var mongodb = final[IntegrationId.MongoDb];
            mongodb.Enabled.Should().BeFalse();

            var msmq = final[IntegrationId.Msmq];
            msmq.Enabled.Should().BeFalse();

            var consmos = final[IntegrationId.CosmosDb];
            consmos.Enabled.Should().BeNull();
        }
    }
}
