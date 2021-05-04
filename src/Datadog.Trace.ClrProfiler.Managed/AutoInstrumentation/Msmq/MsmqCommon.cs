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
        internal const string ServiceName = "msmq";
        internal static readonly IntegrationInfo IntegrationId = IntegrationRegistry.GetIntegrationInfo(IntegrationName);

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(MsmqCommon));

        internal static Scope CreateScope(Tracer tracer, string command, string spanKind, string queueName, string formatName, string queueLabel, DateTime queueLastModifiedTime, bool withinTransaction, string transactionType, bool transactionalQueue, out MsmqTags tags)
        {
            tags = null;
            if (!tracer.Settings.IsIntegrationEnabled(IntegrationId))
            {
                // integration disabled, don't create a scope, skip this trace
                return null;
            }

            Scope scope = null;

            try
            {
                tags = new MsmqTags(spanKind)
                {
                    Queue = queueName,
                    QueueLabel = queueLabel,
                    QueueLastModifiedTime = queueLastModifiedTime.ToLongTimeString(),
                    IsTransactionalQueue = transactionalQueue.ToString(),
                    UniqueQueueName = formatName,
                    TransactionType = transactionType,
                    InstrumentationName = IntegrationName
                };
                scope = tracer.StartActiveWithTags(command, serviceName: ServiceName, tags: tags);

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
