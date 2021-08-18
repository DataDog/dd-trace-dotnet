using System;
using Automatonymous;
using MassTransit.Autofac.Saga.Contracts;

namespace MassTransit.Autofac.Saga.Components
{
    public class OrderStateMachine :
        MassTransitStateMachine<OrderState>
    {
        public OrderStateMachine()
        {
            Event(() => OrderReceived, x => x.CorrelateById(m => m.Message.OrderId));

            InstanceState(x => x.CurrentState);

            Initially(
                When(OrderReceived)
                    .ThenAsync(context => Console.Out.WriteLineAsync($"OrderState: Order Received: {context.Data.OrderId}"))
                    .Then((context) =>
                    {
                        context.Instance.OrderSubmissionDateTime = context.Data.OrderDateTime;
                        context.Instance.OrderSubmissionDateTimeUtc = context.Data.OrderDateTime.DateTime.ToUniversalTime();
                    })
                    .Activity(s => s.OfInstanceType<PublishOrderEventActivity>())
                    .TransitionTo(Submitted));
        }

        public Event<OrderReceived> OrderReceived { get; private set; }

        public State Submitted { get; private set; }
        public State Accepted { get; private set; }
    }
}