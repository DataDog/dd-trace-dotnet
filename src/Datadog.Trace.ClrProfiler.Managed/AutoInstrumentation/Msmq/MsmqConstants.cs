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
        internal const string MsmqCursorHandle = "System.Messaging.Interop.CursorHandle";
        internal const string MsmqMessageQueueTransaction = "System.Messaging.MessageQueueTransaction";
        internal const string MsmqMessageQueueTransactionType = "System.Messaging.MessageQueueTransactionType";
        internal const string MsmqMessagePropertyFilter = "System.Messaging.MessagePropertyFilter";
        internal const string MsmqMessage = "System.Messaging.Message";
        internal const string MsmqPurgeCommand = "msmq.purge";
        internal const string MsmqSendCommand = "msmq.send";
        internal const string MsmqReceiveCommand = "msmq.receive";
        internal const string MsmqPeekCommand = "msmq.peek";
        internal static readonly IntegrationInfo IntegrationId = IntegrationRegistry.GetIntegrationInfo(IntegrationName);
    }
}
