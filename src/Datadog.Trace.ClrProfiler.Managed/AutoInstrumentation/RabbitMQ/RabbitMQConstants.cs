using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.RabbitMQ
{
    internal static class RabbitMQConstants
    {
        internal const string IntegrationName = nameof(IntegrationIds.RabbitMQ);
        internal const string IBasicPropertiesTypeName = "RabbitMQ.Client.IBasicProperties";
        internal const string IDictionaryArgumentsTypeName = "System.Collections.Generic.IDictionary`2[System.String,System.Object]";
        internal const string OperationName = "amqp.command";
        internal const string ServiceName = "rabbitmq";
        internal static readonly IntegrationInfo IntegrationId = IntegrationRegistry.GetIntegrationInfo(IntegrationName);
    }
}
