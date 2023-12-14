// <copyright file="RemotingIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK

#nullable enable

using System;
using System.Runtime.Remoting.Messaging;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Remoting
{
    internal static class RemotingIntegration
    {
        internal const IntegrationId IntegrationId = Configuration.IntegrationId.Remoting;
        internal const string IntegrationName = nameof(Configuration.IntegrationId.Remoting);

        internal const string Major4 = "4";

        private const string ClientOperationName = "dotnet_remoting.client.request";
        private const string ServerOperationName = "dotnet_remoting.server.request";
        private const string ServiceName = "remoting";

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(RemotingIntegration));

        internal static Scope? CreateServerScope(IMessage msg, SpanContext? spanContext)
        {
            var tracer = Tracer.Instance;

            if (!tracer.Settings.IsIntegrationEnabled(IntegrationId))
            {
                // integration disabled, don't create a scope, skip this trace
                return null;
            }

            Scope? scope = null;

            try
            {
                var tags = new RemotingServerTags();
                scope = tracer.StartActiveInternal(ServerOperationName, parent: spanContext, tags: tags);
                var span = scope.Span;

                var methodMessage = msg as IMethodMessage;
                tags.MethodName = methodMessage?.MethodName;
                // tags.MethodService = methodMessage?.MethodMes
                span.ResourceName = methodMessage?.MethodName;

                tags.SetAnalyticsSampleRate(IntegrationId, tracer.Settings, enabledWithGlobalSetting: true);
                tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating scope.");
            }

            return scope;
        }

        internal static Scope? CreateClientScope(IMessage msg)
        {
            var tracer = Tracer.Instance;

            if (!tracer.Settings.IsIntegrationEnabled(IntegrationId))
            {
                // integration disabled, don't create a scope, skip this trace
                return null;
            }

            var serviceName = tracer.CurrentTraceSettings.GetServiceName(tracer, ServiceName);

            Scope? scope = null;

            try
            {
                var clientSchema = tracer.CurrentTraceSettings.Schema.Client;
                var tags = clientSchema.CreateRemotingClientTags();
                scope = tracer.StartActiveInternal(ClientOperationName, serviceName: serviceName, tags: tags);
                var span = scope.Span;

                var methodMessage = msg as IMethodMessage;
                tags.MethodName = methodMessage?.MethodName;
                // tags.MethodService = methodMessage?.MethodMes
                span.ResourceName = methodMessage?.MethodName;

                tags.SetAnalyticsSampleRate(IntegrationId, tracer.Settings, enabledWithGlobalSetting: false);
                tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating scope.");
            }

            return scope;
        }
    }
}
#endif
