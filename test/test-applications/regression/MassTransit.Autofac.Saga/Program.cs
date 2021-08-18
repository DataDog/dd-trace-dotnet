using System;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using GreenPipes;
using MassTransit;
using MassTransit.Saga;
using MassTransit.Util;
using MassTransit.Autofac.Saga.Components;
using MassTransit.Autofac.Saga.Contracts;

namespace MassTransit.Autofac.Saga
{
    static class Program
    {
        internal static readonly int NumMessagesToSend = 5;
        internal static readonly int MessageSendDelayMs = 500;
        internal static readonly CountdownEvent Countdown = new CountdownEvent(NumMessagesToSend);

        static void Main()
        {
            var container = ConfigureContainer();

            var bus = container.Resolve<IBusControl>();

            try
            {
                bus.Start();
                try
                {
                    for (int i = 0; i < NumMessagesToSend; i++)
                    {
                        TaskUtil.Await(() => Submit(container));
                        Console.WriteLine($"Message #{i} sent.");
                        Thread.Sleep(MessageSendDelayMs);
                    }

                    Countdown.Wait(5000);
                }
                finally
                {
                    bus.Stop();
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
            }
        }

        static async Task Submit(IContainer container)
        {
            IBus bus = container.Resolve<IBus>();

            var orderId = NewId.NextGuid();

            await bus.Send<SubmitOrder>(new
            {
                OrderId = orderId,
                OrderDateTime = DateTimeOffset.Now
            }, Pipe.Execute<SendContext>(sendContext => sendContext.ConversationId = sendContext.CorrelationId = orderId));
        }

        static IContainer ConfigureContainer()
        {
            var builder = new ContainerBuilder();
            builder.AddMassTransit(cfg =>
            {
                cfg.AddConsumersFromNamespaceContaining<SubmitOrderConsumer>();
                cfg.AddSagaStateMachinesFromNamespaceContaining(typeof(OrderStateMachine));

                cfg.AddBus(BusFactory);
            });

            builder.RegisterType<PublishOrderEventActivity>();
            builder.RegisterInMemorySagaRepository();

            return builder.Build();
        }

        static IBusControl BusFactory(IComponentContext context)
        {
            return Bus.Factory.CreateUsingInMemory(cfg => cfg.ConfigureEndpoints(context));
        }
    }
}