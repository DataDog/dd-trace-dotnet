using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Google.Api.Gax;
using Google.Api.Gax.Grpc;
using Google.Cloud.PubSub.V1;

namespace Samples.DataStreams.GoogleCloudPubSub
{
    public static class Program
    {
        private const int MessageCount = 5;
        private const string ProjectId = "test-project";
        private const string BaggageKey = "dsm-pubsub-baggage-key";
        private const string BaggageValue = "dsm-pubsub-baggage-value";

        public static async Task Main(string[] args)
        {
            var runId = args.Length > 0 ? args[0] : Guid.NewGuid().ToString("N");
            var topicId = $"dsm-pubsub-topic-{runId}";
            var subscriptionId = $"dsm-pubsub-subscription-{runId}";

            var emulatorHost = Environment.GetEnvironmentVariable("PUBSUB_EMULATOR_HOST");
            if (string.IsNullOrEmpty(emulatorHost))
            {
                Console.WriteLine("PUBSUB_EMULATOR_HOST is not set; nothing to do.");
                return;
            }

            Console.WriteLine($"Topic: {topicId}");
            Console.WriteLine($"Subscription: {subscriptionId}");

            var publisherApi = new PublisherServiceApiClientBuilder { EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
            var subscriberApi = new SubscriberServiceApiClientBuilder { EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();

            try
            {
                publisherApi.CreateTopic(new TopicName(ProjectId, topicId));
            }
            catch (Grpc.Core.RpcException e) when (e.StatusCode == Grpc.Core.StatusCode.AlreadyExists)
            {
            }

            try
            {
                subscriberApi.CreateSubscription(
                    new SubscriptionName(ProjectId, subscriptionId),
                    new TopicName(ProjectId, topicId),
                    pushConfig: null,
                    ackDeadlineSeconds: 60);
            }
            catch (Grpc.Core.RpcException e) when (e.StatusCode == Grpc.Core.StatusCode.AlreadyExists)
            {
            }

            await using var publisher = new PublisherClientBuilder
            {
                TopicName = new TopicName(ProjectId, topicId),
                EmulatorDetection = EmulatorDetection.EmulatorOnly,
            }.Build();

            await using var subscriber = new SubscriberClientBuilder
            {
                SubscriptionName = new SubscriptionName(ProjectId, subscriptionId),
                EmulatorDetection = EmulatorDetection.EmulatorOnly,
            }.Build();

            var consumed = 0;
            var allConsumed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var consumeTask = subscriber.StartAsync((message, cancellationToken) =>
            {
                var number = message.Data.ToStringUtf8();
                Datadog.Trace.Baggage.Current.TryGetValue(BaggageKey, out var baggageValue);
                Console.WriteLine($"Consumed: {number}, baggage: {baggageValue ?? "<none>"}");
                if (Interlocked.Increment(ref consumed) >= MessageCount)
                {
                    allConsumed.TrySetResult(true);
                }

                return Task.FromResult(SubscriberClient.Reply.Ack);
            });

            Datadog.Trace.Baggage.Current = new Dictionary<string, string?> { [BaggageKey] = BaggageValue };

            for (var i = 0; i < MessageCount; i++)
            {
                var messageId = await publisher.PublishAsync(new PubsubMessage
                {
                    Data = Google.Protobuf.ByteString.CopyFromUtf8($"message {i}"),
                });
                Console.WriteLine($"Published message {i}: {messageId}");
            }

            var timeout = Task.Delay(TimeSpan.FromSeconds(120));
            var finished = await Task.WhenAny(allConsumed.Task, timeout);
            if (finished != allConsumed.Task)
            {
                Console.WriteLine("Timed out waiting for messages");
                Environment.ExitCode = 1;
            }

            await subscriber.DisposeAsync();
            if (!consumeTask.IsCompleted)
            {
                await Task.WhenAny(consumeTask, Task.Delay(TimeSpan.FromSeconds(30)));
            }

            Console.WriteLine($"Done, consumed {consumed} messages");
        }
    }
}
