using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;

namespace Samples.AzureServiceBus.Legacy
{
    public static class Program
    {
        private const string ConnectionString = "Endpoint=sb://servicebus.localhost.invalid/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=c2FtcGxlLWtleQ==";
        private const string PathwayHeader = "dd-pathway-ctx-base64";
        private const string Queue = "samples-legacy-queue";
        private const string Topic = "samples-legacy-topic";
        private const string Subscription = Topic + "/Subscriptions/samples-legacy-subscription";
        private const string ForwardTopic = "samples-legacy-forward-topic";

        public static async Task<int> Main()
        {
            var queueMessages = new List<Message>();
            var topicMessages = new List<Message>();

            await SendWithoutNetwork(Queue, queueMessages, 1, (sender, messages) => sender.SendAsync(messages[0]));
            await SendWithoutNetwork(Queue, queueMessages, 2, (sender, messages) => sender.SendAsync(messages));
            await SendWithoutNetwork(Queue, queueMessages, 1, (sender, messages) => sender.ScheduleMessageAsync(messages[0], DateTimeOffset.UtcNow.AddMinutes(5)));
            await SendWithoutNetwork(Topic, topicMessages, 1, (sender, messages) => sender.SendAsync(messages[0]));

            var received = await ReceiveWithoutNetwork(Queue, queueMessages);
            var forwardResult = await ReceiveAndForwardWithoutNetwork(Subscription, topicMessages, ForwardTopic);
            received += forwardResult.Received;

            var all = queueMessages.Concat(topicMessages).ToList();
            var withHeader = all.Count(m => m.UserProperties.ContainsKey(PathwayHeader));
            Console.WriteLine($"Received {received} of {all.Count} messages");
            Console.WriteLine($"Messages with {PathwayHeader}: {withHeader} of {all.Count}");
            Console.WriteLine($"Forwarded messages with a new {PathwayHeader}: {forwardResult.WithNewPathway} of {forwardResult.Forwarded}");

            return received == all.Count ? 0 : 1;
        }

        private static async Task SendWithoutNetwork(string entityPath, List<Message> sent, int count, Func<MessageSender, IList<Message>, Task> send)
        {
            var messages = Enumerable.Range(0, count)
                                     .Select(_ => new Message(Encoding.UTF8.GetBytes("legacy-sdk-message")) { MessageId = Guid.NewGuid().ToString() })
                                     .ToList();
            var sender = new MessageSender(ConnectionString, entityPath);
            sender.RegisterPlugin(new StopBeforeNetworkPlugin());

            try
            {
                await send(sender, messages);
                throw new InvalidOperationException("The send was expected to stop before reaching the network");
            }
            catch (StopBeforeNetworkException)
            {
                sent.AddRange(messages);
            }
        }

        private static async Task<int> ReceiveWithoutNetwork(string entityPath, IList<Message> messages)
        {
            var receiver = new InMemoryMessageReceiver(ConnectionString, entityPath, messages);
            var received = await receiver.ReceiveAsync(messages.Count, TimeSpan.FromSeconds(5));
            return received?.Count ?? 0;
        }

        private static async Task<ForwardResult> ReceiveAndForwardWithoutNetwork(
            string entityPath,
            IList<Message> messages,
            string forwardPath)
        {
            var result = new ForwardResult();
            var allMessagesForwarded = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var sender = new MessageSender(ConnectionString, forwardPath);
            sender.RegisterPlugin(new StopBeforeNetworkPlugin());
            var receiver = new InMemoryMessageReceiver(ConnectionString, entityPath, messages);

            receiver.RegisterMessageHandler(
                async (message, cancellationToken) =>
                {
                    Interlocked.Increment(ref result.Received);
                    var previousPathway = message.UserProperties?[PathwayHeader] as string;

                    try
                    {
                        await sender.SendAsync(new[] { message });
                        throw new InvalidOperationException("The forward was expected to stop before reaching the network");
                    }
                    catch (StopBeforeNetworkException)
                    {
                        Interlocked.Increment(ref result.Forwarded);
                        var newPathway = message.UserProperties?[PathwayHeader] as string;
                        if (previousPathway is not null && newPathway is not null && previousPathway != newPathway)
                        {
                            Interlocked.Increment(ref result.WithNewPathway);
                        }
                    }

                    if (result.Received == messages.Count)
                    {
                        allMessagesForwarded.TrySetResult(true);
                    }
                },
                new MessageHandlerOptions(_ => Task.CompletedTask)
                {
                    AutoComplete = false,
                    MaxConcurrentCalls = 1
                });

            await Task.WhenAny(allMessagesForwarded.Task, Task.Delay(TimeSpan.FromSeconds(30)));
            return result;
        }

        private sealed class ForwardResult
        {
            public int Received;
            public int Forwarded;
            public int WithNewPathway;
        }

        private sealed class StopBeforeNetworkPlugin : ServiceBusPlugin
        {
            public override string Name => nameof(StopBeforeNetworkPlugin);

            public override Task<Message> BeforeMessageSend(Message message) => throw new StopBeforeNetworkException();
        }

        private sealed class StopBeforeNetworkException : Exception
        {
        }

        private sealed class InMemoryMessageReceiver : MessageReceiver
        {
            private readonly Queue<Message> _pending;

            public InMemoryMessageReceiver(string connectionString, string entityPath, IList<Message> pending)
                : base(new ServiceBusConnectionStringBuilder(connectionString) { EntityPath = entityPath }, ReceiveMode.ReceiveAndDelete)
            {
                _pending = new Queue<Message>(pending);
            }

            protected override async Task<IList<Message>> OnReceiveAsync(int maxMessageCount, TimeSpan serverWaitTime)
            {
                var messages = new List<Message>(maxMessageCount);
                while (messages.Count < maxMessageCount && _pending.TryDequeue(out var message))
                {
                    messages.Add(message);
                }

                if (messages.Count == 0)
                {
                    await Task.Delay(100);
                    return null;
                }

                return messages;
            }
        }
    }
}
