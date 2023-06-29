// <copyright file="MsmqCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Msmq
{
    internal static class MsmqCommon
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(MsmqCommon));

        internal static Scope CreateScope<TMessageQueue>(Tracer tracer, string command, string spanKind, TMessageQueue messageQueue, bool? messagePartofTransaction = null)
            where TMessageQueue : IMessageQueue
        {
            if (!tracer.Settings.IsIntegrationEnabled(MsmqConstants.IntegrationId))
            {
                // integration disabled, don't create a scope, skip this trace
                return null;
            }

            Scope scope = null;

            try
            {
                string operationName = tracer.CurrentTraceSettings.Schema.Messaging.GetOutboundCommandOperationName(MsmqConstants.MessagingType);
                string serviceName = tracer.CurrentTraceSettings.Schema.Messaging.GetOutboundServiceName(MsmqConstants.MessagingType);
                MsmqTags tags = tracer.CurrentTraceSettings.Schema.Messaging.CreateMsmqTags(spanKind);

                tags.Command = command;
                tags.IsTransactionalQueue = messageQueue.Transactional.ToString();
                tags.Host = messageQueue.MachineName;
                tags.Path = messageQueue.Path;
                if (messagePartofTransaction.HasValue)
                {
                    tags.MessageWithTransaction = messagePartofTransaction.ToString();
                }

                scope = tracer.StartActiveInternal(operationName, serviceName: serviceName, tags: tags);

                var span = scope.Span;
                span.Type = SpanTypes.Queue;
                span.ResourceName = $"{command} {messageQueue.Path}";
                tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(MsmqConstants.IntegrationId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating scope.");
            }

            return scope;
        }
    }
}
