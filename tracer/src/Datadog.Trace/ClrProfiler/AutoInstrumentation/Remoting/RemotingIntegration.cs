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

        // sinkStack is stable across one request/response cycle regardless of the formatter chain shape,
        // so it's a reliable key for correlating ProcessMessage's scope with the later SerializeResponse call.
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
                // AddOrUpdate isn't available on the .NET Framework ConditionalWeakTable API surface
                ServerScopesBySinkStack.Remove(sinkStack);
                ServerScopesBySinkStack.Add(sinkStack, scope);
            }
        }

        // A preceding sink that didn't recognize the content-type may have left a placeholder scope for
        // this sinkStack. Discard it (without sending its span) before creating the real one, while it's
        // still the active scope - disposing it later would leave the ambient scope chain unpoppable.
        internal static void DiscardStalePlaceholderScope(object sinkStack)
        {
            if (ServerScopesBySinkStack.TryGetValue(sinkStack, out var previousScope) && previousScope is not null)
            {
                previousScope.SetFinishOnClose(false);
                previousScope.Dispose();
                ServerScopesBySinkStack.Remove(sinkStack);
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
