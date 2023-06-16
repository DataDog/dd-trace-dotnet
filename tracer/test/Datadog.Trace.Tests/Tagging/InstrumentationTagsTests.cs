// <copyright file="InstrumentationTagsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.ClrProfiler.AutoInstrumentation.Elasticsearch;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.MongoDb;
using Datadog.Trace.Tagging;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Tagging
{
    public class InstrumentationTagsTests
    {
        [Fact]
        public void HttpV1Tags_PeerService_PopulatesFromHost()
        {
            var host = "localhost";
            var tags = new HttpV1Tags();

            tags.SetHost(host);

            tags.PeerService.Should().Be(host);
            tags.PeerServiceSource.Should().Be("network.destination.name");
        }

        [Fact]
        public void HttpV1Tags_PeerService_PopulatesFromCustom()
        {
            var customService = "client-service";
            var tags = new HttpV1Tags();

            tags.SetTag("peer.service", customService);

            tags.PeerService.Should().Be(customService);
            tags.PeerServiceSource.Should().Be("peer.service");
        }

        [Fact]
        public void HttpV1Tags_PeerService_CustomTakesPrecedence()
        {
            var customService = "client-service";
            var host = "localhost";
            var tags = new HttpV1Tags();

            tags.SetTag("peer.service", customService);
            tags.SetHost(host);

            tags.PeerService.Should().Be(customService);
            tags.PeerServiceSource.Should().Be("peer.service");
        }

        [Fact]
        public void KafkaV1Tags_PeerService_PopulatesFromBootstrapServers()
        {
            var bootstrapServer = "localhost";
            var tags = new KafkaV1Tags(SpanKinds.Consumer);

            tags.BootstrapServers = bootstrapServer;

            tags.PeerService.Should().Be(bootstrapServer);
            tags.PeerServiceSource.Should().Be(Trace.Tags.KafkaBootstrapServers);
        }

        [Fact]
        public void KafkaV1Tags_PeerService_PopulatesFromCustom()
        {
            var customService = "client-service";
            var tags = new KafkaV1Tags(SpanKinds.Consumer);

            tags.SetTag("peer.service", customService);

            tags.PeerService.Should().Be(customService);
            tags.PeerServiceSource.Should().Be("peer.service");
        }

        [Fact]
        public void KafkaV1Tags_PeerService_CustomTakesPrecedence()
        {
            var customService = "client-service";
            var bootstrapServer = "localhost";
            var tags = new KafkaV1Tags(SpanKinds.Consumer);

            tags.SetTag("peer.service", customService);
            tags.BootstrapServers = bootstrapServer;

            tags.PeerService.Should().Be(customService);
            tags.PeerServiceSource.Should().Be("peer.service");
        }

        [Fact]
        public void ElasticsearchV1Tags_PeerService_PopulatesFromDestinationHost()
        {
            var hostName = "host";
            var tags = new ElasticsearchV1Tags();

            tags.Host = hostName;

            tags.PeerService.Should().Be(hostName);
            tags.PeerServiceSource.Should().Be("network.destination.name");
        }

        [Fact]
        public void ElasticsearchV1Tags_PeerService_PopulatesFromCustom()
        {
            var customService = "client-service";
            var tags = new ElasticsearchV1Tags();

            tags.SetTag("peer.service", customService);

            tags.PeerService.Should().Be(customService);
            tags.PeerServiceSource.Should().Be("peer.service");
        }

        [Fact]
        public void ElasticsearchV1Tags_PeerService_CustomTakesPrecedence()
        {
            var hostName = "host";
            var customService = "client-service";
            var tags = new ElasticsearchV1Tags();

            tags.SetTag("peer.service", customService);
            tags.Host = hostName;

            tags.PeerService.Should().Be(customService);
            tags.PeerServiceSource.Should().Be("peer.service");
        }

        [Fact]
        public void MongoDbV1Tags_PeerService_PopulatesFromDbName()
        {
            var databaseName = "database";
            var tags = new MongoDbV1Tags();

            tags.DbName = databaseName;

            tags.PeerService.Should().Be(databaseName);
            tags.PeerServiceSource.Should().Be("db.instance");
        }

        [Fact]
        public void MongoDbV1Tags_PeerService_PopulatesFromDestinationHost()
        {
            var hostName = "host";
            var tags = new MongoDbV1Tags();

            tags.Host = hostName;

            tags.PeerService.Should().Be(hostName);
            tags.PeerServiceSource.Should().Be("network.destination.name");
        }

        [Fact]
        public void MongoDbV1Tags_PeerService_PopulatesFromCustom()
        {
            var customService = "client-service";
            var tags = new MongoDbV1Tags();

            tags.SetTag("peer.service", customService);

            tags.PeerService.Should().Be(customService);
            tags.PeerServiceSource.Should().Be("peer.service");
        }

        [Fact]
        public void MongoDbV1Tags_PeerService_CustomTakesPrecedence()
        {
            var databaseName = "database";
            var hostName = "host";
            var customService = "client-service";
            var tags = new MongoDbV1Tags();

            tags.SetTag("peer.service", customService);
            tags.DbName = databaseName;
            tags.Host = hostName;

            tags.PeerService.Should().Be(customService);
            tags.PeerServiceSource.Should().Be("peer.service");
        }

        [Fact]
        public void SqlV1Tags_PeerService_PopulatesFromOutHost()
        {
            var host = "localhost";
            var tags = new SqlV1Tags();

            tags.OutHost = host;

            tags.PeerService.Should().Be(host);
            tags.PeerServiceSource.Should().Be("network.destination.name");
        }

        [Fact]
        public void SqlV1Tags_PeerService_PopulatesFromDbName()
        {
            var databaseName = "database";
            var tags = new SqlV1Tags();

            tags.DbName = databaseName;

            tags.PeerService.Should().Be(databaseName);
            tags.PeerServiceSource.Should().Be("db.instance");
        }

        [Fact]
        public void SqlV1Tags_PeerService_PopulatesFromCustom()
        {
            var customService = "client-service";
            var tags = new SqlV1Tags();

            tags.SetTag("peer.service", customService);

            tags.PeerService.Should().Be(customService);
            tags.PeerServiceSource.Should().Be("peer.service");
        }

        [Fact]
        public void SqlV1Tags_PeerService_CustomTakesPrecedenceOverRest()
        {
            var customService = "client-service";
            var host = "localhost";
            var databaseName = "database";
            var tags = new SqlV1Tags();

            tags.SetTag("peer.service", customService);
            tags.DbName = databaseName;
            tags.OutHost = host;

            tags.PeerService.Should().Be(customService);
            tags.PeerServiceSource.Should().Be("peer.service");
        }

        [Fact]
        public void SqlV1Tags_PeerService_DbNameTakesPrecedenceOverOutHost()
        {
            var host = "localhost";
            var databaseName = "database";
            var tags = new SqlV1Tags();

            tags.DbName = databaseName;
            tags.OutHost = host;

            tags.PeerService.Should().Be(databaseName);
            tags.PeerServiceSource.Should().Be("db.instance");
        }
    }
}
