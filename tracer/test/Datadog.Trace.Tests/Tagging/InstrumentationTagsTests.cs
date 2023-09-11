// <copyright file="InstrumentationTagsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.ClrProfiler.AutoInstrumentation.Elasticsearch;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.MongoDb;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Redis;
using Datadog.Trace.ServiceFabric;
using Datadog.Trace.Tagging;
using Datadog.Trace.TestHelpers.FluentAssertionsExtensions;
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

            tags.Host = host;

            tags.PeerService.Should().Be(host);
            tags.PeerServiceSource.Should().Be("out.host");
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
            tags.Host = host;

            tags.PeerService.Should().Be(customService);
            tags.PeerServiceSource.Should().Be("peer.service");
            tags.GetTag(Tags.PeerServiceRemappedFrom).Should().BeNull();
        }

        [Fact]
        public void KafkaV1Tags_PeerService_PopulatesFromBootstrapServers()
        {
            var bootstrapServer = "localhost";
            var tags = new KafkaV1Tags(SpanKinds.Producer);

            tags.BootstrapServers = bootstrapServer;

            tags.PeerService.Should().Be(bootstrapServer);
            tags.PeerServiceSource.Should().Be(Trace.Tags.KafkaBootstrapServers);
        }

        [Fact]
        public void KafkaV1Tags_PeerService_PopulatesFromCustom()
        {
            var customService = "client-service";
            var tags = new KafkaV1Tags(SpanKinds.Producer);

            tags.SetTag("peer.service", customService);

            tags.PeerService.Should().Be(customService);
            tags.PeerServiceSource.Should().Be("peer.service");
        }

        [Fact]
        public void KafkaV1Tags_PeerService_CustomTakesPrecedence()
        {
            var customService = "client-service";
            var bootstrapServer = "localhost";
            var tags = new KafkaV1Tags(SpanKinds.Producer);

            tags.SetTag("peer.service", customService);
            tags.BootstrapServers = bootstrapServer;

            tags.PeerService.Should().Be(customService);
            tags.PeerServiceSource.Should().Be("peer.service");
            tags.GetTag(Tags.PeerServiceRemappedFrom).Should().BeNull();
        }

        [Fact]
        public void KafkaV1Tags_PeerService_ConsumerHasNoPeerService()
        {
            var bootstrapServer = "localhost";
            var tags = new KafkaV1Tags(SpanKinds.Consumer);

            tags.BootstrapServers = bootstrapServer;

            tags.PeerService.Should().BeNull();
            tags.PeerServiceSource.Should().BeNull();
        }

        [Fact]
        public void MsmqV1Tags_PeerService_PopulatesFromOutHost()
        {
            var host = ".";
            var tags = new MsmqV1Tags(SpanKinds.Producer);

            tags.Host = host;

            tags.PeerService.Should().Be(host);
            tags.PeerServiceSource.Should().Be(Trace.Tags.OutHost);
        }

        [Fact]
        public void MsmqV1Tags_PeerService_PopulatesFromCustom()
        {
            var customService = "client-service";
            var tags = new MsmqV1Tags(SpanKinds.Producer);

            tags.SetTag("peer.service", customService);

            tags.PeerService.Should().Be(customService);
            tags.PeerServiceSource.Should().Be("peer.service");
        }

        [Fact]
        public void MsmqV1Tags_PeerService_CustomTakesPrecedence()
        {
            var customService = "client-service";
            var host = ".";
            var tags = new MsmqV1Tags(SpanKinds.Producer);

            tags.SetTag("peer.service", customService);
            tags.Host = host;

            tags.PeerService.Should().Be(customService);
            tags.PeerServiceSource.Should().Be("peer.service");
            tags.GetTag(Tags.PeerServiceRemappedFrom).Should().BeNull();
        }

        [Fact]
        public void MsmqV1Tags_PeerService_ConsumerHasNoPeerService()
        {
            var host = ".";
            var tags = new MsmqV1Tags(SpanKinds.Consumer);

            tags.Host = host;

            tags.PeerService.Should().BeNull();
            tags.PeerServiceSource.Should().BeNull();
        }

        [Fact]
        public void ElasticsearchV1Tags_PeerService_PopulatesFromDestinationHost()
        {
            var hostName = "host";
            var tags = new ElasticsearchV1Tags();

            tags.Host = hostName;

            tags.PeerService.Should().Be(hostName);
            tags.PeerServiceSource.Should().Be("out.host");
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
            tags.GetTag(Tags.PeerServiceRemappedFrom).Should().BeNull();
        }

        [Fact]
        public void CouchbaseV1Tags_PeerService_PopulatesFromSeedNodes()
        {
            var nodes = "node1:port1,node2:port2";
            var tags = new CouchbaseV1Tags();

            tags.SeedNodes = nodes;

            tags.PeerService.Should().Be(nodes);
            tags.PeerServiceSource.Should().Be("db.couchbase.seed.nodes");
        }

        [Fact]
        public void CouchbaseV1Tags_PeerService_PopulatesFromDestinationHost()
        {
            var hostName = "host";
            var tags = new CouchbaseV1Tags();

            tags.Host = hostName;

            tags.PeerService.Should().Be(hostName);
            tags.PeerServiceSource.Should().Be("out.host");
        }

        [Fact]
        public void CouchbaseV1Tags_PeerService_PopulatesFromCustom()
        {
            var customService = "client-service";
            var tags = new CouchbaseV1Tags();

            tags.SetTag("peer.service", customService);

            tags.PeerService.Should().Be(customService);
            tags.PeerServiceSource.Should().Be("peer.service");
        }

        [Fact]
        public void CouchbaseV1Tags_PeerService_CustomTakesPrecedence()
        {
            var nodes = "node1:port1,node2:port2";
            var hostName = "host";
            var customService = "client-service";
            var tags = new CouchbaseV1Tags();

            tags.SetTag("peer.service", customService);
            tags.SeedNodes = nodes;
            tags.Host = hostName;

            tags.PeerService.Should().Be(customService);
            tags.PeerServiceSource.Should().Be("peer.service");
            tags.GetTag(Tags.PeerServiceRemappedFrom).Should().BeNull();
        }

        [Fact]
        public void CouchbaseV1Tags_PeerService_SeedNodesTakesPrecedenceOverOutHost()
        {
            var nodes = "node1:port1,node2:port2";
            var hostName = "host";
            var tags = new CouchbaseV1Tags();

            tags.SeedNodes = nodes;
            tags.Host = hostName;

            tags.PeerService.Should().Be(nodes);
            tags.PeerServiceSource.Should().Be("db.couchbase.seed.nodes");
        }

        [Fact]
        public void MongoDbV1Tags_PeerService_PopulatesFromDbName()
        {
            var databaseName = "database";
            var tags = new MongoDbV1Tags();

            tags.DbName = databaseName;

            tags.PeerService.Should().Be(databaseName);
            tags.PeerServiceSource.Should().Be("db.name");
        }

        [Fact]
        public void MongoDbV1Tags_PeerService_PopulatesFromDestinationHost()
        {
            var hostName = "host";
            var tags = new MongoDbV1Tags();

            tags.Host = hostName;

            tags.PeerService.Should().Be(hostName);
            tags.PeerServiceSource.Should().Be("out.host");
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
            tags.GetTag(Tags.PeerServiceRemappedFrom).Should().BeNull();
        }

        [Fact]
        public void SqlV1Tags_PeerService_PopulatesFromOutHost()
        {
            var host = "localhost";
            var tags = new SqlV1Tags();

            tags.OutHost = host;

            tags.PeerService.Should().Be(host);
            tags.PeerServiceSource.Should().Be("out.host");
        }

        [Fact]
        public void SqlV1Tags_PeerService_PopulatesFromDbName()
        {
            var databaseName = "database";
            var tags = new SqlV1Tags();

            tags.DbName = databaseName;

            tags.PeerService.Should().Be(databaseName);
            tags.PeerServiceSource.Should().Be("db.name");
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
            tags.GetTag(Tags.PeerServiceRemappedFrom).Should().BeNull();
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
            tags.PeerServiceSource.Should().Be("db.name");
        }

        [Fact]
        public void GrpcClientV1Tags_PeerService_PopulatesFromRpcService()
        {
            var service = "grpc-app";
            var tags = new GrpcClientV1Tags();

            tags.MethodService = service;

            tags.PeerService.Should().Be(service);
            tags.PeerServiceSource.Should().Be("rpc.service");
        }

        [Fact]
        public void GrpcClientV1Tags_PeerService_PopulatesFromDestinationHost()
        {
            var hostName = "host";
            var tags = new GrpcClientV1Tags();

            tags.Host = hostName;

            tags.PeerService.Should().Be(hostName);
            tags.PeerServiceSource.Should().Be("out.host");
        }

        [Fact]
        public void GrpcClientV1Tags_PeerService_PopulatesFromCustom()
        {
            var customService = "client-service";
            var tags = new GrpcClientV1Tags();

            tags.SetTag("peer.service", customService);

            tags.PeerService.Should().Be(customService);
            tags.PeerServiceSource.Should().Be("peer.service");
        }

        [Fact]
        public void GrpcClientV1Tags_PeerService_CustomTakesPrecedence()
        {
            var service = "grpc-app";
            var hostName = "host";
            var customService = "client-service";
            var tags = new GrpcClientV1Tags();

            tags.SetTag("peer.service", customService);
            tags.MethodService = service;
            tags.Host = hostName;

            tags.PeerService.Should().Be(customService);
            tags.PeerServiceSource.Should().Be("peer.service");
            tags.GetTag(Tags.PeerServiceRemappedFrom).Should().BeNull();
        }

        [Fact]
        public void ServiceRemotingClientV1Tags_PeerService_PopulatesFromRemotingService()
        {
            var service = "HelloWorld/Service";
            var tags = new ServiceRemotingClientV1Tags();

            tags.RemotingServiceName = service;

            tags.PeerService.Should().Be(service);
            tags.PeerServiceSource.Should().Be("service-fabric.service-remoting.service");
        }

        [Fact]
        public void ServiceRemotingClientV1Tags_PeerService_PopulatesFromRemotingUri()
        {
            var uri = "fabric:/HelloWorld/Service";
            var tags = new ServiceRemotingClientV1Tags();

            tags.RemotingUri = uri;

            tags.PeerService.Should().Be(uri);
            tags.PeerServiceSource.Should().Be("service-fabric.service-remoting.uri");
        }

        [Fact]
        public void ServiceRemotingClientV1Tags_PeerService_PopulatesFromCustom()
        {
            var customService = "client-service";
            var tags = new ServiceRemotingClientV1Tags();

            tags.SetTag("peer.service", customService);

            tags.PeerService.Should().Be(customService);
            tags.PeerServiceSource.Should().Be("peer.service");
        }

        [Fact]
        public void ServiceRemotingClientV1Tags_PeerService_RemotingServiceTakesPrecedenceOverRemotingUri()
        {
            var uri = "fabric:/HelloWorld/Service";
            var service = "HelloWorld/Service";
            var tags = new ServiceRemotingClientV1Tags();

            tags.RemotingUri = uri;
            tags.RemotingServiceName = service;

            tags.PeerService.Should().Be(service);
            tags.PeerServiceSource.Should().Be("service-fabric.service-remoting.service");
            tags.GetTag(Tags.PeerServiceRemappedFrom).Should().BeNull();
        }

        [Fact]
        public void ServiceRemotingClientV1Tags_PeerService_CustomTakesPrecedence()
        {
            var uri = "fabric:/HelloWorld/Service";
            var service = "HelloWorld/Service";
            var customService = "client-service";
            var tags = new ServiceRemotingClientV1Tags();

            tags.SetTag("peer.service", customService);
            tags.RemotingUri = uri;
            tags.RemotingServiceName = service;

            tags.PeerService.Should().Be(customService);
            tags.PeerServiceSource.Should().Be("peer.service");
            tags.GetTag(Tags.PeerServiceRemappedFrom).Should().BeNull();
        }

        [Fact]
        public void AzureServiceBusTags_ReceiveMessagingOperation_ReturnsSpanKindConsumer()
        {
            var spanKind = "client";
            var tags = new AzureServiceBusTags();

            tags.SetTag("span.kind", spanKind);
            tags.SetTag("messaging.operation", "publish");
            tags.GetTag("span.kind").Should().Be(spanKind);

            // Set messaging operation to invoke our custom behavior
            tags.SetTag("messaging.operation", "receive");
            tags.GetTag("span.kind").Should().Be("consumer");
        }

        [Fact]
        public void AzureServiceBusV1Tags_PeerService_NotSetForConsumer()
        {
            var sourceName = "source";
            var tags = new AzureServiceBusV1Tags();

            tags.SetTag("span.kind", "consumer");
            tags.SetTag("messaging.source.name", sourceName); // Set via SetTag to mimic Activity usage

            tags.PeerService.Should().BeNull();
            tags.PeerServiceSource.Should().BeNull();
            tags.GetTag(Tags.PeerServiceRemappedFrom).Should().BeNull();
        }

        [Fact]
        public void AzureServiceBusV1Tags_PeerService_PopulatesFromMessagingDestinationName()
        {
            var destinationName = "destination";
            var tags = new AzureServiceBusV1Tags();

            tags.SetTag("messaging.destination.name", destinationName); // Set via SetTag to mimic Activity usage

            tags.PeerService.Should().Be(destinationName);
            tags.PeerServiceSource.Should().Be("messaging.destination.name");
            tags.GetTag(Tags.PeerServiceRemappedFrom).Should().BeNull();
        }

        [Fact]
        public void AzureServiceBusV1Tags_PeerService_PopulatesFromEitherMessagingSourceOrDestinationName()
        {
            var sourceName = "source";
            var destinationName = "destination";
            var tags = new AzureServiceBusV1Tags();

            tags.SetTag("messaging.source.name", sourceName); // Set via SetTag to mimic Activity usage
            tags.SetTag("messaging.destination.name", destinationName); // Set via SetTag to mimic Activity usage

            tags.PeerService.Should().BeOneOf(sourceName, destinationName);
            tags.PeerServiceSource.Should().BeOneOf("messaging.source.name", "messaging.destination.name");
            tags.GetTag(Tags.PeerServiceRemappedFrom).Should().BeNull();
        }

        [Fact]
        public void AzureServiceBusV1Tags_PeerService_PopulatesFromCustom()
        {
            var customService = "client-service";
            var tags = new AzureServiceBusV1Tags();

            tags.SetTag("peer.service", customService);

            tags.PeerService.Should().Be(customService);
            tags.PeerServiceSource.Should().Be("peer.service");
            tags.GetTag(Tags.PeerServiceRemappedFrom).Should().BeNull();
        }

        [Fact]
        public void AzureServiceBusV1Tags_PeerService_CustomTakesPrecedence()
        {
            var sourceName = "source";
            var destinationName = "destination";
            var customService = "client-service";
            var tags = new AzureServiceBusV1Tags();

            tags.SetTag("peer.service", customService);
            tags.SetTag("messaging.source.name", sourceName); // Set via SetTag to mimic Activity usage
            tags.SetTag("messaging.destination.name", destinationName); // Set via SetTag to mimic Activity usage

            tags.PeerService.Should().Be(customService);
            tags.PeerServiceSource.Should().Be("peer.service");
            tags.GetTag(Tags.PeerServiceRemappedFrom).Should().BeNull();
        }

        [Fact]
        public void RedisV1Tags_PeerService_PopulatesFromOutHost()
        {
            var host = "localhost";
            var tags = new RedisV1Tags();

            tags.Host = host;

            tags.PeerService.Should().Be(host);
            tags.PeerServiceSource.Should().Be("out.host");
        }

        [Fact]
        public void RedisV1Tags_PeerService_PopulatesFromCustom()
        {
            var customService = "client-service";
            var tags = new RedisV1Tags();

            tags.SetTag("peer.service", customService);

            tags.PeerService.Should().Be(customService);
            tags.PeerServiceSource.Should().Be("peer.service");
        }

        [Fact]
        public void RedisV1Tags_PeerService_CustomTakesPrecedenceOverRest()
        {
            var customService = "client-service";
            var host = "localhost";
            var tags = new RedisV1Tags();

            tags.SetTag("peer.service", customService);
            tags.Host = host;

            tags.PeerService.Should().Be(customService);
            tags.PeerServiceSource.Should().Be("peer.service");
            tags.GetTag(Tags.PeerServiceRemappedFrom).Should().BeNull();
        }

        [Fact]
        public void CosmosDbV1Tags_PeerService_PopulatesFromOutHost()
        {
            var host = "localhost";
            var tags = new CosmosDbV1Tags();

            tags.Host = host;

            tags.PeerService.Should().Be(host);
            tags.PeerServiceSource.Should().Be("out.host");
        }

        [Fact]
        public void CosmosDbV1Tags_PeerService_PopulatesFromDbName()
        {
            var databaseName = "database";
            var tags = new CosmosDbV1Tags();

            tags.DatabaseId = databaseName;

            tags.PeerService.Should().Be(databaseName);
            tags.PeerServiceSource.Should().Be("db.name");
        }

        [Fact]
        public void CosmosDbV1Tags_PeerService_PopulatesFromCustom()
        {
            var customService = "client-service";
            var tags = new CosmosDbV1Tags();

            tags.SetTag("peer.service", customService);

            tags.PeerService.Should().Be(customService);
            tags.PeerServiceSource.Should().Be("peer.service");
        }

        [Fact]
        public void CosmosDbV1Tags_PeerService_CustomTakesPrecedenceOverRest()
        {
            var customService = "client-service";
            var host = "localhost";
            var databaseName = "database";
            var tags = new CosmosDbV1Tags();

            tags.SetTag("peer.service", customService);
            tags.DatabaseId = databaseName;
            tags.Host = host;

            tags.PeerService.Should().Be(customService);
            tags.PeerServiceSource.Should().Be("peer.service");
            tags.GetTag(Tags.PeerServiceRemappedFrom).Should().BeNull();
        }

        [Fact]
        public void CosmosDbV1Tags_PeerService_DbNameTakesPrecedenceOverOutHost()
        {
            var host = "localhost";
            var databaseName = "database";
            var tags = new CosmosDbV1Tags();

            tags.DatabaseId = databaseName;
            tags.Host = host;

            tags.PeerService.Should().Be(databaseName);
            tags.PeerServiceSource.Should().Be("db.name");
        }

        [Fact]
        public void AerospikeV1Tags_PeerService_PopulatesFromOutHost()
        {
            var ns = "ns1";
            var tags = new AerospikeV1Tags();

            tags.Namespace = ns;

            tags.PeerService.Should().Be(ns);
            tags.PeerServiceSource.Should().Be("aerospike.namespace");
        }

        [Fact]
        public void AerospikeV1Tags_PeerService_PopulatesFromCustom()
        {
            var customService = "client-service";
            var tags = new AerospikeV1Tags();

            tags.SetTag("peer.service", customService);

            tags.PeerService.Should().Be(customService);
            tags.PeerServiceSource.Should().Be("peer.service");
        }

        [Fact]
        public void AerospikeV1Tags_PeerService_CustomTakesPrecedenceOverRest()
        {
            var customService = "client-service";
            var ns = "ns1";
            var tags = new AerospikeV1Tags();

            tags.SetTag("peer.service", customService);
            tags.Namespace = ns;

            tags.PeerService.Should().Be(customService);
            tags.PeerServiceSource.Should().Be("peer.service");
        }
    }
}
