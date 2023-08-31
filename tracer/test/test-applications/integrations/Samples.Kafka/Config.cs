using System;
using System.Collections.Generic;
using Confluent.Kafka;

namespace Samples.Kafka
{
    internal static class Config
    {
        public static readonly string KafkaBrokerHost = Environment.GetEnvironmentVariable("KAFKA_BROKER_HOST") ?? "localhost:9092";

        public static ClientConfig Create()
        {
            return new ClientConfig
            {
                BootstrapServers = KafkaBrokerHost
            };
        }
    }
}
