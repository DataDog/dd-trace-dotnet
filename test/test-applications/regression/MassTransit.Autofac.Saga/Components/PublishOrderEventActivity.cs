using System;
using System.Threading.Tasks;
using Automatonymous;
using GreenPipes;
using MassTransit;
using MassTransit.Autofac.Saga.Contracts;

namespace MassTransit.Autofac.Saga.Components
{
    public class PublishOrderEventActivity :
        Activity<OrderState>
    {
        readonly IPublishEndpoint _publishEndpoint;

        public PublishOrderEventActivity(ConsumeContext publishEndpoint)
        {
            _publishEndpoint = publishEndpoint;
        }

        public void Probe(ProbeContext context)
        {
            context.CreateScope("publish-order-event");
        }

        public void Accept(StateMachineVisitor visitor)
        {
            visitor.Visit(this);
        }

        public async Task Execute(BehaviorContext<OrderState> context, Behavior<OrderState> next)
        {
            await Console.Out.WriteLineAsync(
                $"Publishing Order Event Created: {context.Instance.CorrelationId} ({context.GetPayload<ConsumeContext>().ConversationId})");

            await _publishEndpoint.Publish<OrderStateCreated>(new
            {
                OrderId = context.Instance.CorrelationId,
                Timestamp = DateTime.UtcNow
            }, sendContext => sendContext.CorrelationId = NewId.NextGuid());

            await next.Execute(context);
        }

        public async Task Execute<T>(BehaviorContext<OrderState, T> context, Behavior<OrderState, T> next)
        {
            await Console.Out.WriteLineAsync(
                $"Publishing Order Event Created: {context.Instance.CorrelationId} ({context.GetPayload<ConsumeContext>().ConversationId})");

            await _publishEndpoint.Publish<OrderStateCreated>(new
            {
                OrderId = context.Instance.CorrelationId,
                Timestamp = DateTime.UtcNow
            }, sendContext => sendContext.CorrelationId = NewId.NextGuid());

            await next.Execute(context);
        }

        public Task Faulted<TException>(BehaviorExceptionContext<OrderState, TException> context, Behavior<OrderState> next)
            where TException : Exception
        {
            return next.Faulted(context);
        }

        public Task Faulted<T, TException>(BehaviorExceptionContext<OrderState, T, TException> context, Behavior<OrderState, T> next)
            where TException : Exception
        {
            return next.Faulted(context);
        }
    }
}