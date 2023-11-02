// <copyright file="KafkaConstants.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka
{
    internal static class KafkaConstants
    {
        internal const string IntegrationName = nameof(Configuration.IntegrationId.Kafka);
        internal const string TopicPartitionTypeName = "Confluent.Kafka.TopicPartition";
        internal const string MessageTypeName = "Confluent.Kafka.Message`2[!0,!1]";
        internal const string ConsumeResultTypeName = "Confluent.Kafka.ConsumeResult`2[!0,!1]";
        internal const string TopicPartitionOffsetEnumerableTypeName = $"System.Collections.Generic.IEnumerable`1[Confluent.Kafka.TopicPartitionOffset]";
        internal const string TopicPartitionOffsetListTypeName = "System.Collections.Generic.IList`1[Confluent.Kafka.TopicPartitionOffset]";
        internal const string ActionOfDeliveryReportTypeName = "System.Action`1[Confluent.Kafka.DeliveryReport`2[!0,!1]]";
        internal const string TaskDeliveryReportTypeName = "System.Threading.Tasks.Task`1[Confluent.Kafka.DeliveryReport`2[!0,!1]]";
        internal const IntegrationId IntegrationId = Configuration.IntegrationId.Kafka;
    }
}
