using Datadog.Trace.Configuration;

namespace Datadog.Trace.ServiceFabric
{
    internal static class ServiceRemotingConstants
    {
        public const string AssemblyName = "Microsoft.ServiceFabric.Services.Remoting";

        public const string ClientEventsTypeName = "Microsoft.ServiceFabric.Services.Remoting.V2.Client.ServiceRemotingClientEvents";

        public const string ServiceEventsTypeName = "Microsoft.ServiceFabric.Services.Remoting.V2.Runtime.ServiceRemotingServiceEvents";

        public const string SendRequestEventName = "SendRequest";

        public const string ReceiveResponseEventName = "ReceiveResponse";

        public const string ReceiveRequestEventName = "ReceiveRequest";

        public const string SendResponseEventName = "SendResponse";

        public static readonly IntegrationInfo IntegrationId = IntegrationRegistry.GetIntegrationInfo(nameof(IntegrationIds.ServiceRemoting));
    }
}
