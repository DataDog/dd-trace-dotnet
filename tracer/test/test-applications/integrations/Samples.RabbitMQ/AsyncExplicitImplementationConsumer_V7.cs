#if RABBITMQ_7_0
using System;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Samples.RabbitMQ;

class AsyncExplicitImplementationConsumer : IAsyncBasicConsumer
{
    private readonly AsyncDefaultBasicConsumer _consumer;
    private readonly IChannel _model;

    public AsyncExplicitImplementationConsumer(IChannel model)
    {
        _consumer = new AsyncDefaultBasicConsumer(model);
        _model = model;
    }

    public IChannel Model => _model;

    public IChannel Channel => _consumer.Channel;

    public event AsyncEventHandler<BasicDeliverEventArgs> ReceivedAsync;

    public async Task HandleBasicCancelAsync(string consumerTag, CancellationToken cancellationToken = new())
    {
        await _consumer.HandleBasicCancelAsync(consumerTag, cancellationToken);
    }

    public async Task HandleBasicCancelOkAsync(string consumerTag, CancellationToken cancellationToken = new())
    {
        await _consumer.HandleBasicCancelOkAsync(consumerTag, cancellationToken);
    }

    public async Task HandleBasicConsumeOkAsync(string consumerTag, CancellationToken cancellationToken = new())
    {
        await _consumer.HandleBasicConsumeOkAsync(consumerTag, cancellationToken);
    }

    public async Task HandleBasicDeliverAsync(string consumerTag, ulong deliveryTag, bool redelivered, string exchange, string routingKey, IReadOnlyBasicProperties properties, ReadOnlyMemory<byte> body, CancellationToken cancellationToken = new())
    {
        await Task.Yield();
        ReceivedAsync?.Invoke(
            this,
            new BasicDeliverEventArgs(consumerTag, deliveryTag, redelivered, exchange, routingKey, properties, body));
    }

    public async Task HandleChannelShutdownAsync(object channel, ShutdownEventArgs reason)
    {
        await _consumer.HandleChannelShutdownAsync(channel, reason);
    }

}
#endif
