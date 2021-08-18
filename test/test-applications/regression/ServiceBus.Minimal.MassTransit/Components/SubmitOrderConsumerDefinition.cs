using GreenPipes;
using MassTransit;
using MassTransit.ConsumeConfigurators;
using MassTransit.Definition;
using ServiceBus.Minimal.MassTransit.Contracts;

namespace ServiceBus.Minimal.MassTransit.Components
{
    public class SubmitOrderConsumerDefinition :
        ConsumerDefinition<SubmitOrderConsumer>
    {
        protected override void ConfigureConsumer(IReceiveEndpointConfigurator endpointConfigurator,
            IConsumerConfigurator<SubmitOrderConsumer> consumerConfigurator)
        {
            endpointConfigurator.UseInMemoryOutbox();
            endpointConfigurator.UseMessageRetry(r => r.Interval(3, 1000));

            EndpointConvention.Map<SubmitOrder>(endpointConfigurator.InputAddress);
        }
    }
}