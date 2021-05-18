using System;
using System.Text;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Msmq
{
    internal static class MsmqCommon
    {
        internal static readonly IntegrationInfo IntegrationId = IntegrationRegistry.GetIntegrationInfo(MsmqConstants.IntegrationName);
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(MsmqCommon));

        internal static Scope CreateScope<TMessageQueue>(Tracer tracer, string command, string spanKind, TMessageQueue messageQueue, bool? messagePartofTransaction = null)
            where TMessageQueue : IMessageQueue
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
                    Path = messageQueue.Path,
                };
                if (messagePartofTransaction.HasValue)
                {
                    tags.MessageWithTransaction = messagePartofTransaction.ToString();
                }

                var serviceName = tracer.Settings.GetServiceName(tracer, MsmqConstants.ServiceName);

                scope = tracer.StartActiveWithTags(MsmqConstants.OperationName, serviceName: serviceName, tags: tags);

                var span = scope.Span;
                span.Type = SpanTypes.Queue;
                span.ResourceName = $"{command} {messageQueue.Path}";
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating scope.");
            }

            return scope;
        }
    }
}
