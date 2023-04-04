namespace MassTransitContracts
{
    using MassTransit;
    using MassTransit.RabbitMqTransport;
    using RabbitMQ.Client;


    public static class ConfigurationExtensions
    {
        public static void ConfigureMessageTopology(this IRabbitMqBusFactoryConfigurator configurator)
        {
            configurator.Message<ContentReceived>(x => x.SetEntityName("content.received"));
            configurator.Send<ContentReceived>(x =>
            {
                x.UseCorrelationId(context => context.Id);
                x.UseRoutingKeyFormatter(context => context.Message.NodeId);
            });

            configurator.Publish<ContentReceived>(x =>
            {
                x.ExchangeType = ExchangeType.Direct;
            });
        }
    }
}
