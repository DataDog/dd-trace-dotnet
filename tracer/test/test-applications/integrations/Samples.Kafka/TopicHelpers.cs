using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Confluent.Kafka;
using Confluent.Kafka.Admin;

namespace Samples.Kafka
{
    internal static class TopicHelpers
    {
        /// <summary>
        /// Tries to create a topic
        /// </summary>
        /// <returns>True if created a new topic, false if the topic already exists</returns>
        public static async Task<bool> TryCreateTopic(
            string topicName,
            int numPartitions,
            short replicationFactor,
            ClientConfig config)
        {
            var attempts = 3;
            bool? created = null;
            do
            {
                using var adminClient = new AdminClientBuilder(config).Build();
                try
                {
                    Console.WriteLine($"Trying to create topic {topicName}...");

                    await adminClient.CreateTopicsAsync(new List<TopicSpecification> {
                        new()
                        {
                            Name = topicName,
                            NumPartitions = numPartitions,
                            ReplicationFactor = replicationFactor
                        }
                    });

                    Console.WriteLine($"Topic created");
                    created = true;
                    break;
                }
                catch (CreateTopicsException e)
                {
                    if (e.Results[0].Error.Code == ErrorCode.TopicAlreadyExists)
                    {
                        Console.WriteLine("Topic already exists");
                        created = false;
                        break;
                    }

                    Console.WriteLine($"An error occured creating topic {topicName}: {e.Results[0].Error.Reason}");
                }
            } while (!TopicExists(topicName, config) && attempts-- > 0);

            if (created is null)
            {
                throw new Exception("Unable to create topic");
            }

            // The create call returns as soon as the broker acks the request, but
            // metadata propagation can lag behind. Wait until the topic is visible
            // with the expected partition count, otherwise downstream callers that
            // immediately query metadata (e.g. to pick a partition) can race and
            // see a topic with zero partitions.
            if (!await WaitForTopicMetadata(topicName, numPartitions, config, TimeSpan.FromSeconds(30)))
            {
                throw new Exception($"Topic {topicName} metadata did not propagate with {numPartitions} partitions");
            }

            return created.Value;
        }

        private static async Task<bool> WaitForTopicMetadata(string topicName, int expectedPartitions, ClientConfig config, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                using var adminClient = new AdminClientBuilder(config).Build();
                var metadata = adminClient.GetMetadata(topicName, TimeSpan.FromSeconds(5));
                var topic = metadata.Topics.FirstOrDefault(t => string.Equals(t.Topic, topicName, StringComparison.Ordinal));
                if (topic != null && topic.Error.Code == ErrorCode.NoError && topic.Partitions.Count == expectedPartitions)
                {
                    return true;
                }

                Console.WriteLine($"Waiting for topic {topicName} metadata (partitions: {topic?.Partitions.Count ?? 0}/{expectedPartitions})...");
                await Task.Delay(TimeSpan.FromMilliseconds(500));
            }

            return false;
        }

        /// <summary>
        /// Tries to delete a topic
        /// </summary>
        /// <returns>True if created a new topic, false if the topic already exists</returns>
        public static async Task TryDeleteTopic(string topicName, ClientConfig config)
        {
            var attempts = 3;
            do
            {
                using var adminClient = new AdminClientBuilder(config).Build();
                try
                {
                    Console.WriteLine($"Trying to delete topic {topicName}...");

                    await adminClient.DeleteTopicsAsync(new[] { topicName });
                    if (!TopicExists(topicName, config))
                    {
                        return;
                    }
                }
                catch (DeleteTopicsException e)
                {
                    if (e.Results[0].Error.Code == ErrorCode.UnknownTopicOrPart)
                    {
                        Console.WriteLine("Topic did not exist, skipping");
                        return;
                    }

                    Console.WriteLine($"An error occured deleting the topic {topicName}: {e.Results[0].Error.Reason}");
                }

            } while (attempts-- > 0);
            throw new Exception("Unable to delete topic");
        }

        private static bool TopicExists(string topicName, ClientConfig config)
        {
            using var adminClient = new AdminClientBuilder(config).Build();
            var metadata = adminClient.GetMetadata(timeout: TimeSpan.FromSeconds(30));

            var topic = metadata.Topics.FirstOrDefault(x => string.Equals(x.Topic, topicName, StringComparison.Ordinal));

            if (topic != null)
            {
                if (topic.Error.Code == ErrorCode.NoError)
                {
                    return true;
                }

                Console.WriteLine($"Topic found with error {topic.Error} (code: {topic.Error.Code})");
            }

            return false;
        }
    }
}
