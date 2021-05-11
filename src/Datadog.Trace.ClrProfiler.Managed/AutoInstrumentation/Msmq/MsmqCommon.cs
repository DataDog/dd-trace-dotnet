using System;
using System.Text;
using Datadog.Trace.ClrProfiler.Integrations;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Msmq
{
    internal class MsmqCommon
    {
        internal const string IntegrationName = nameof(IntegrationIds.Msmq);
        internal static readonly IntegrationInfo IntegrationId = IntegrationRegistry.GetIntegrationInfo(IntegrationName);

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(MsmqCommon));

        internal static Scope CreateScope(Tracer tracer, string command, string spanKind, IMessageQueue messageQueue, bool? messagePartofTransaction = null)
        {
            if (!tracer.Settings.IsIntegrationEnabled(IntegrationId))
            {
                // integration disabled, don't create a scope, skip this trace
                return null;
            }

            Scope scope = null;

            try
            {
                var tags = new MsmqTags(spanKind)
                {
                    Command = command,
                    IsTransactionalQueue = messageQueue.Transactional.ToString(),
                    UniqueQueueName = messageQueue.FormatName,
                };
                if (messagePartofTransaction.HasValue)
                {
                    tags.MessageWithTransaction = messagePartofTransaction.ToString();
                }

                var serviceName = tracer.Settings.GetServiceName(tracer, MsmqConstants.ServiceName);

                scope = tracer.StartActiveWithTags(MsmqConstants.OperationName, serviceName: serviceName, tags: tags);

                var span = scope.Span;
                span.Type = SpanTypes.Queue;
                span.ResourceName = command;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating scope.");
            }

            return scope;
        }
    }
}
