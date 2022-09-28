using System;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Samples.RabbitMQ
{
    // Implement a custom consumer class whose implementation is nearly
    // identical to the implementation of RabbitMQ.Client.Events.EventingBasicConsumer.
    // This will test that we can properly instrument implicit interface implementations
    // of RabbitMQ.Client.IBasicConsumer.HandleBasicDeliver
    class SyncImplicitImplementationConsumer : IBasicConsumer
    {
        private readonly DefaultBasicConsumer _consumer;
        private readonly IModel _model;

        public SyncImplicitImplementationConsumer(IModel model)
        {
            _consumer = new DefaultBasicConsumer(model);
            _model = model;
        }

        public IModel Model => _model;

        public event EventHandler<BasicDeliverEventArgs> Received;

        public event EventHandler<ConsumerEventArgs> ConsumerCancelled;

        public void HandleBasicCancel(string consumerTag) =>
            _consumer.HandleBasicCancel(consumerTag);

        public void HandleBasicCancelOk(string consumerTag) => _consumer.HandleBasicCancelOk(consumerTag);

        public void HandleBasicConsumeOk(string consumerTag) => _consumer.HandleBasicConsumeOk(consumerTag);

        public void HandleBasicDeliver(string consumerTag, ulong deliveryTag, bool redelivered, string exchange, string routingKey, IBasicProperties properties, ReadOnlyMemory<byte> body)
        {
            _consumer.HandleBasicDeliver(consumerTag, deliveryTag, redelivered, exchange, routingKey, properties, body);
            Received?.Invoke(
                this,
                new BasicDeliverEventArgs(consumerTag, deliveryTag, redelivered, exchange, routingKey, properties, body));
        }

        public void HandleModelShutdown(object model, ShutdownEventArgs reason) => _consumer.HandleModelShutdown(model, reason);
    }
}
