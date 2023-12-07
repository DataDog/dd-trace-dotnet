// <copyright file="KafkaFixture.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace Datadog.Trace.TestHelpers.Containers;

public class KafkaFixture : ContainerFixture
{
    protected IContainer BrokerContainer => GetResource<IContainer>("broker");

    public override IEnumerable<KeyValuePair<string, string>> GetEnvironmentVariables()
    {
        yield return new("KAFKA_BROKER_HOST", $"{BrokerContainer.Hostname}:{BrokerContainer.GetMappedPublicPort(9092)}");
    }

    protected override async Task InitializeResources(Action<string, object> registerResource)
    {
        var network = new NetworkBuilder()
            .WithName("kafka")
            .WithCleanUp(true)
            .Build();

        var zookeeperContainer = new ContainerBuilder()
            .WithImage("confluentinc/cp-zookeeper:6.1.1")
            .WithHostname("kafka-zookeeper")
            .WithEnvironment("ZOOKEEPER_CLIENT_PORT", "2181")
            .WithEnvironment("ZOOKEEPER_TICK_TIME", "2000")
            .WithNetwork(network)
            .Build();

        var kafkaBrokerContainer = new ContainerBuilder()
            .WithImage("confluentinc/cp-server:6.1.1")
            .WithHostname("kafka-broker")
            .WithPortBinding(9092, 9092)
            .WithPortBinding(9101, 9101)
            .WithEnvironment("KAFKA_BROKER_ID", "1")
            .WithEnvironment("KAFKA_ZOOKEEPER_CONNECT", "kafka-zookeeper:2181")
            .WithEnvironment("KAFKA_LISTENER_SECURITY_PROTOCOL_MAP", "PLAINTEXT:PLAINTEXT,PLAINTEXT_HOST:PLAINTEXT")
            .WithEnvironment("KAFKA_AUTO_CREATE_TOPICS_ENABLE", "false")
            .WithEnvironment("KAFKA_ADVERTISED_LISTENERS", "PLAINTEXT://kafka-broker:29092,PLAINTEXT_HOST://localhost:9092")
            .WithEnvironment("KAFKA_METRIC_REPORTERS", "io.confluent.metrics.reporter.ConfluentMetricsReporter")
            .WithEnvironment("KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR", "1")
            .WithEnvironment("KAFKA_GROUP_INITIAL_REBALANCE_DELAY_MS", "0")
            .WithEnvironment("KAFKA_CONFLUENT_LICENSE_TOPIC_REPLICATION_FACTOR", "1")
            .WithEnvironment("KAFKA_CONFLUENT_BALANCER_TOPIC_REPLICATION_FACTOR", "1")
            .WithEnvironment("KAFKA_TRANSACTION_STATE_LOG_MIN_ISR", "1")
            .WithEnvironment("KAFKA_TRANSACTION_STATE_LOG_REPLICATION_FACTOR", "1")
            .WithEnvironment("KAFKA_JMX_PORT", "9101")
            .WithEnvironment("KAFKA_JMX_HOSTNAME", "localhost")
            .WithEnvironment("KAFKA_CONFLUENT_SCHEMA_REGISTRY_URL", "http://kafka-schema-registry:8081")
            .WithEnvironment("CONFLUENT_METRICS_REPORTER_BOOTSTRAP_SERVERS", "kafka-broker:29092")
            .WithEnvironment("CONFLUENT_METRICS_REPORTER_TOPIC_REPLICAS", "1")
            .WithEnvironment("CONFLUENT_METRICS_ENABLE", "true")
            .WithEnvironment("CONFLUENT_SUPPORT_CUSTOMER_ID", "anonymous")
            .DependsOn(zookeeperContainer)
            .WithNetwork(network)
            .Build();

        var schemaRegistryContainer = new ContainerBuilder()
            .WithImage("confluentinc/cp-schema-registry:6.1.1")
            .WithHostname("kafka-schema-registry")
            .WithEnvironment("SCHEMA_REGISTRY_HOST_NAME", "kafka-schema-registry")
            .WithEnvironment("SCHEMA_REGISTRY_KAFKASTORE_BOOTSTRAP_SERVERS", "kafka-broker:29092")
            .WithEnvironment("SCHEMA_REGISTRY_LISTENERS", "http://0.0.0.0:8081")
            .DependsOn(kafkaBrokerContainer)
            .WithNetwork(network)
            .Build();

        var controlCenterContainer = new ContainerBuilder()
            .WithImage("confluentinc/cp-enterprise-control-center:6.1.1")
            .WithHostname("kafka-control-center")
            .WithEnvironment("CONTROL_CENTER_BOOTSTRAP_SERVERS", "kafka-broker:29092")
            .WithEnvironment("CONTROL_CENTER_SCHEMA_REGISTRY_URL", "http://kafka-schema-registry:8081")
            .WithEnvironment("CONTROL_CENTER_REPLICATION_FACTOR", "1")
            .WithEnvironment("CONTROL_CENTER_INTERNAL_TOPICS_PARTITIONS", "1")
            .WithEnvironment("CONTROL_CENTER_MONITORING_INTERCEPTOR_TOPIC_PARTITIONS", "1")
            .WithEnvironment("CONFLUENT_METRICS_TOPIC_REPLICATION", "1")
            .WithEnvironment("PORT", "9021")
            .DependsOn(kafkaBrokerContainer)
            .DependsOn(schemaRegistryContainer)
            .WithNetwork(network)
            .Build();

        var restProxyContainer = new ContainerBuilder()
            .WithImage("confluentinc/cp-kafka-rest:6.1.1")
            .WithHostname("kafka-rest-proxy")
            .WithEnvironment("KAFKA_REST_HOST_NAME", "kafka-rest-proxy")
            .WithEnvironment("KAFKA_REST_BOOTSTRAP_SERVERS", "kafka-broker:29092")
            .WithEnvironment("KAFKA_REST_LISTENERS", "http://0.0.0.0:8082")
            .WithEnvironment("KAFKA_REST_SCHEMA_REGISTRY_URL", "http://kafka-schema-registry:8081")
            .DependsOn(kafkaBrokerContainer)
            .DependsOn(schemaRegistryContainer)
            .WithNetwork(network)
            .Build();

        await zookeeperContainer.StartAsync();
        await kafkaBrokerContainer.StartAsync();
        await schemaRegistryContainer.StartAsync();
        await controlCenterContainer.StartAsync();
        await restProxyContainer.StartAsync();

        registerResource("zookeeper", zookeeperContainer);
        registerResource("broker", kafkaBrokerContainer);
        registerResource("schema-registry", schemaRegistryContainer);
        registerResource("control-center", controlCenterContainer);
        registerResource("rest-proxy", restProxyContainer);
        registerResource("network", network);
    }
}
