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
                    return true;
                }
                catch (CreateTopicsException e)
                {
                    if (e.Results[0].Error.Code == ErrorCode.TopicAlreadyExists)
                    {
                        Console.WriteLine("Topic already exists");
                        return false;
                    }

                    Console.WriteLine($"An error occured creating topic {topicName}: {e.Results[0].Error.Reason}");
                }
            } while (!TopicExists(topicName, config) && attempts-- > 0);
            throw new Exception("Unable to create topic");
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
            return metadata.Topics.Any(
                x => string.Equals(x.Topic, topicName, StringComparison.Ordinal)
                  && x.Error.Code == ErrorCode.NoError);
        }
    }
}
