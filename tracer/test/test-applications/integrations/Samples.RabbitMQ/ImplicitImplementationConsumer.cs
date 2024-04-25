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

#pragma warning disable CS0067 // The event 'SyncImplicitImplementationConsumer.ConsumerCancelled' is never used
        public event EventHandler<ConsumerEventArgs> ConsumerCancelled;
#pragma warning restore CS0067

        public void HandleBasicCancel(string consumerTag) =>
            _consumer.HandleBasicCancel(consumerTag);

        public void HandleBasicCancelOk(string consumerTag) => _consumer.HandleBasicCancelOk(consumerTag);

        public void HandleBasicConsumeOk(string consumerTag) => _consumer.HandleBasicConsumeOk(consumerTag);

#if RABBITMQ_6_0
        public void HandleBasicDeliver(string consumerTag, ulong deliveryTag, bool redelivered, string exchange, string routingKey, IBasicProperties properties, ReadOnlyMemory<byte> body)
        {
            _consumer.HandleBasicDeliver(consumerTag, deliveryTag, redelivered, exchange, routingKey, properties, body);
            Received?.Invoke(
                this,
                new BasicDeliverEventArgs(consumerTag, deliveryTag, redelivered, exchange, routingKey, properties, body));
        }
#else
        public void HandleBasicDeliver(string consumerTag, ulong deliveryTag, bool redelivered, string exchange, string routingKey, IBasicProperties properties, byte[] body)
        {
            _consumer.HandleBasicDeliver(consumerTag, deliveryTag, redelivered, exchange, routingKey, properties, body);
            Received?.Invoke(
                this,
                new BasicDeliverEventArgs(consumerTag, deliveryTag, redelivered, exchange, routingKey, properties, body));
        }
#endif

        public void HandleModelShutdown(object model, ShutdownEventArgs reason) => _consumer.HandleModelShutdown(model, reason);
    }
}
