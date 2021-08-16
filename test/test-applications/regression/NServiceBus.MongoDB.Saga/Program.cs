using System;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus.MongoDB.Saga.Shared;

namespace NServiceBus.MongoDB.Saga
{
    class Program
    {
        internal static readonly int NumMessagesToSend = 5;
        internal static readonly int MessageSendDelayMs = 500;
        internal static readonly int MessageCompletionDurationMs = 1000;
        internal static readonly CountdownEvent Countdown = new CountdownEvent(NumMessagesToSend);

        static async Task Main()
        {
            #region mongoDbConfig

            var endpointConfiguration = new EndpointConfiguration("Samples.MongoDB.Server");
            var persistence = endpointConfiguration.UsePersistence<MongoPersistence>().UseTransactions(false);
            persistence.DatabaseName("Samples_MongoDB_Server");

            #endregion

            endpointConfiguration.EnableInstallers();
            endpointConfiguration.UseTransport<LearningTransport>();

            var endpointInstance = await Endpoint.Start(endpointConfiguration)
                .ConfigureAwait(false);

            await SendMessagesAsync();

            await endpointInstance.Stop()
                .ConfigureAwait(false);
        }

        static async Task SendMessagesAsync()
        {
            var endpointConfiguration = new EndpointConfiguration("Samples.MongoDB.Client");
            endpointConfiguration.UsePersistence<LearningPersistence>();
            endpointConfiguration.UseTransport<LearningTransport>();

            var endpointInstance = await Endpoint.Start(endpointConfiguration)
                .ConfigureAwait(false);

            Console.WriteLine($"Sending {NumMessagesToSend} messages with a {MessageSendDelayMs}ms delay.");

            for (int i = 0; i < NumMessagesToSend; i++)
            {
                var orderId = Guid.NewGuid();
                var startOrder = new StartOrder
                {
                    OrderId = orderId
                };

                await endpointInstance.Send("Samples.MongoDB.Server", startOrder)
                    .ConfigureAwait(false);

                Console.WriteLine($"Message #{i}: StartOrder Message sent with OrderId {orderId}");

                await Task.Delay(MessageSendDelayMs);
            }

            await endpointInstance.Stop()
                .ConfigureAwait(false);

            Countdown.Wait(5000);

            // Wait one more second to ensure the state is flushed between consecutive local runs
            await Task.Delay(1000);
        }
    }
}
