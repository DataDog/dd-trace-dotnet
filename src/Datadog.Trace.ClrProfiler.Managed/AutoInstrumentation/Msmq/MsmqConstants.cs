using System;
using System.Text;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Msmq
{
    internal static class MsmqConstants
    {
        internal const string IntegrationName = nameof(IntegrationIds.Msmq);
        internal const string OperationName = "msmq.command";
        internal const string ServiceName = "msmq";
        internal static readonly IntegrationInfo IntegrationId = IntegrationRegistry.GetIntegrationInfo(IntegrationName);
    }
}
