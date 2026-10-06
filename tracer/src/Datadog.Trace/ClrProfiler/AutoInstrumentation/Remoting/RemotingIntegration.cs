// <copyright file="RemotingIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK

#nullable enable

using System;
using System.Runtime.CompilerServices;
using System.Runtime.Remoting.Messaging;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Propagators;
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

        // One server scope per request, keyed by the sinkStack (stable across the whole request/response cycle,
        // whatever the formatter chain looks like): the first ProcessMessage creates it, later sinks in the chain
        // reuse it, and SerializeResponse (or the first ProcessMessage, if no response is serialized) closes it.
        private static readonly ConditionalWeakTable<object, Scope> ServerScopesBySinkStack = new();

        internal static Scope? CreateServerScope(IMessage? msg, PropagationContext context)
        {
            var tracer = Tracer.Instance;

            if (!tracer.CurrentTraceSettings.Settings.IsIntegrationEnabled(IntegrationId))
            {
                // integration disabled, don't create a scope, skip this trace
                return null;
            }

            Scope? scope = null;

            try
            {
                var tags = new RemotingServerTags();
                scope = tracer.StartActiveInternal(ServerOperationName, parent: context.SpanContext, tags: tags);
                var span = scope.Span;

                var methodMessage = msg as IMethodMessage;
                tags.MethodName = methodMessage?.MethodName;
                // tags.MethodService = methodMessage?.MethodMes
                span.ResourceName = methodMessage?.MethodName;

                tags.SetAnalyticsSampleRate(IntegrationId, tracer.CurrentTraceSettings.Settings, enabledWithGlobalSetting: true);
                tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating scope.");
            }

            return scope;
        }

        internal static void SetServerScope(object sinkStack, Scope? scope)
        {
            if (scope is not null)
            {
                ServerScopesBySinkStack.Add(sinkStack, scope);
            }
        }

        internal static bool TryGetServerScope(object sinkStack, out Scope? scope)
        {
            return ServerScopesBySinkStack.TryGetValue(sinkStack, out scope);
        }

        // The request message isn't always available when the scope is created (a formatter that still has
        // to deserialize the request is called without one), so fill in the method name once we have it.
        internal static void SetMethodNameIfMissing(Scope scope, IMessage? msg)
        {
            if (scope.Span.Tags is RemotingTags { MethodName: null } tags && msg is IMethodMessage { MethodName: { } methodName })
            {
                tags.MethodName = methodName;
                scope.Span.ResourceName ??= methodName;
            }
        }

        internal static bool TryGetAndRemoveServerScope(object sinkStack, out Scope? scope)
        {
            if (ServerScopesBySinkStack.TryGetValue(sinkStack, out scope))
            {
                ServerScopesBySinkStack.Remove(sinkStack);
                return true;
            }

            scope = null;
            return false;
        }

        internal static Scope? CreateClientScope(IMessage msg)
        {
            var tracer = Tracer.Instance;

            if (!tracer.CurrentTraceSettings.Settings.IsIntegrationEnabled(IntegrationId))
            {
                // integration disabled, don't create a scope, skip this trace
                return null;
            }

            var (serviceName, serviceNameSource) = tracer.CurrentTraceSettings.GetServiceNameMetadata(ServiceName);

            Scope? scope = null;

            try
            {
                var clientSchema = tracer.CurrentTraceSettings.Schema.Client;
                var tags = clientSchema.CreateRemotingClientTags();
                scope = tracer.StartActiveInternal(ClientOperationName, serviceName: serviceName, serviceNameSource: serviceNameSource, tags: tags);
                var span = scope.Span;

                var methodMessage = msg as IMethodMessage;
                tags.MethodName = methodMessage?.MethodName;
                // tags.MethodService = methodMessage?.MethodMes
                span.ResourceName = methodMessage?.MethodName;

                tags.SetAnalyticsSampleRate(IntegrationId, tracer.CurrentTraceSettings.Settings, enabledWithGlobalSetting: false);
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
