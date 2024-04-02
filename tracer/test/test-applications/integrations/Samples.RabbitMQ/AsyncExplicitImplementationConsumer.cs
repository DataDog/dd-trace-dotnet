#if RABBITMQ_5_0
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Samples.RabbitMQ
{
    // Implement a custom consumer class whose implementation is nearly
    // identical to the implementation of RabbitMQ.Client.Events.EventingBasicConsumer.
    // This will test that we can properly instrument explicit interface implementations
    // of RabbitMQ.Client.IBasicConsumer.HandleBasicDeliver
    class AsyncExplicitImplementationConsumer : IBasicConsumer, IAsyncBasicConsumer
    {
        private readonly AsyncDefaultBasicConsumer _consumer;
        private readonly IModel _model;

        public AsyncExplicitImplementationConsumer(IModel model)
        {
            _consumer = new AsyncDefaultBasicConsumer(model);
            _model = model;
        }

        public IModel Model => _model;

        public event EventHandler<BasicDeliverEventArgs> Received;

#pragma warning disable CS0067 // The event 'AsyncExplicitImplementationConsumer.ConsumerCancelled' is never used
        public event AsyncEventHandler<ConsumerEventArgs> ConsumerCancelled;
#pragma warning restore CS0067

        Task IAsyncBasicConsumer.HandleBasicCancel(string consumerTag) => _consumer.HandleBasicCancel(consumerTag);

        Task IAsyncBasicConsumer.HandleBasicCancelOk(string consumerTag) => _consumer.HandleBasicCancelOk(consumerTag);

        Task IAsyncBasicConsumer.HandleBasicConsumeOk(string consumerTag) => _consumer.HandleBasicConsumeOk(consumerTag);

#if RABBITMQ_6_0
        async Task IAsyncBasicConsumer.HandleBasicDeliver(string consumerTag, ulong deliveryTag, bool redelivered, string exchange, string routingKey, IBasicProperties properties, ReadOnlyMemory<byte> body)
        {
            await Task.Yield();
            Received?.Invoke(
                this,
                new BasicDeliverEventArgs(consumerTag, deliveryTag, redelivered, exchange, routingKey, properties, body));
        }
#else
        async Task IAsyncBasicConsumer.HandleBasicDeliver(string consumerTag, ulong deliveryTag, bool redelivered, string exchange, string routingKey, IBasicProperties properties, byte[] body)
        {
            await Task.Yield();
            Received?.Invoke(
                this,
                new BasicDeliverEventArgs(consumerTag, deliveryTag, redelivered, exchange, routingKey, properties, body));
        }
#endif

        Task IAsyncBasicConsumer.HandleModelShutdown(object model, ShutdownEventArgs reason)
            => _consumer.HandleModelShutdown(model, reason);

        // IBasicConsumer explicit implementations
        // These should all throw InvalidOperationExceptions
        event EventHandler<ConsumerEventArgs> IBasicConsumer.ConsumerCancelled
        {
            add { throw new InvalidOperationException("Should never be called."); }
            remove { throw new InvalidOperationException("Should never be called."); }
        }

        void IBasicConsumer.HandleBasicCancel(string consumerTag)
        {
            throw new InvalidOperationException("Should never be called.");
        }

        void IBasicConsumer.HandleBasicCancelOk(string consumerTag)
        {
            throw new InvalidOperationException("Should never be called.");
        }

        void IBasicConsumer.HandleBasicConsumeOk(string consumerTag)
        {
            throw new InvalidOperationException("Should never be called.");
        }

#if RABBITMQ_6_0
        void IBasicConsumer.HandleBasicDeliver(string consumerTag, ulong deliveryTag, bool redelivered, string exchange, string routingKey, IBasicProperties properties, ReadOnlyMemory<byte> body)
        {
            throw new InvalidOperationException("Should never be called.");
        }
#else
        void IBasicConsumer.HandleBasicDeliver(string consumerTag, ulong deliveryTag, bool redelivered, string exchange, string routingKey, IBasicProperties properties, byte[] body)
        {
            throw new InvalidOperationException("Should never be called.");
        }
#endif

        void IBasicConsumer.HandleModelShutdown(object model, ShutdownEventArgs reason)
        {
            throw new InvalidOperationException("Should never be called.");
        }
    }
}
#endif
