#if RABBITMQ_7_0
using System;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Samples.RabbitMQ;

// Implement a custom consumer class whose implementation is nearly
// identical to the implementation of RabbitMQ.Client.Events.EventingBasicConsumer.
// This will test that we can properly instrument implicit interface implementations
// of RabbitMQ.Client.IBasicConsumer.HandleBasicDeliver
class AsyncImplicitImplementationConsumer(IChannel model) : IAsyncBasicConsumer
{
    private readonly AsyncDefaultBasicConsumer _consumer = new(model);

    public IChannel Channel => _consumer.Channel;

    public event AsyncEventHandler<BasicDeliverEventArgs> ReceivedAsync;

    async Task IAsyncBasicConsumer.HandleBasicCancelAsync(string consumerTag, CancellationToken cancellationToken)
    {
        await _consumer.HandleBasicCancelAsync(consumerTag, cancellationToken);
    }

    async Task IAsyncBasicConsumer.HandleBasicCancelOkAsync(string consumerTag, CancellationToken cancellationToken)
    {
        await _consumer.HandleBasicCancelOkAsync(consumerTag, cancellationToken);
    }

    async Task IAsyncBasicConsumer.HandleBasicConsumeOkAsync(string consumerTag, CancellationToken cancellationToken)
    {
        await _consumer.HandleBasicConsumeOkAsync(consumerTag, cancellationToken);
    }

    async Task IAsyncBasicConsumer.HandleBasicDeliverAsync(string consumerTag, ulong deliveryTag, bool redelivered, string exchange, string routingKey, IReadOnlyBasicProperties properties, ReadOnlyMemory<byte> body, CancellationToken cancellationToken)
    {
        await Task.Yield();
        ReceivedAsync?.Invoke(
            this,
            new BasicDeliverEventArgs(consumerTag, deliveryTag, redelivered, exchange, routingKey, properties, body));
    }

    async Task IAsyncBasicConsumer.HandleChannelShutdownAsync(object channel, ShutdownEventArgs reason)
    {
        await _consumer.HandleChannelShutdownAsync(channel, reason);
    }
}
#endif
