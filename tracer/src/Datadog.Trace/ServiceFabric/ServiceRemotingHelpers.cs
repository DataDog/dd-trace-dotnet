// <copyright file="ServiceRemotingHelpers.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Globalization;
using System.Reflection;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ServiceFabric
{
    internal static class ServiceRemotingHelpers
    {
        private const string SpanNamePrefix = "service_remoting";

        private static readonly Logging.IDatadogLogger Log = Logging.DatadogLogging.GetLoggerFor(typeof(ServiceRemotingHelpers));

        public static bool AddEventHandler(string typeName, string eventName, EventHandler eventHandler)
        {
            string fullEventName = $"{typeName}.{eventName}";

            try
            {
                Type? type = Type.GetType($"{typeName}, {ServiceRemotingConstants.AssemblyName}", throwOnError: false);

                if (type == null)
                {
                    if (PlatformHelpers.ServiceFabric.IsRunningInServiceFabric())
                    {
                        Log.Warning("Could not get type {TypeName}.", typeName);
                    }

                    return false;
                }

                EventInfo? eventInfo = type.GetEvent(eventName, BindingFlags.Static | BindingFlags.Public);

                if (eventInfo == null)
                {
                    if (PlatformHelpers.ServiceFabric.IsRunningInServiceFabric())
                    {
                        Log.Warning("Could not get event {EventName}.", fullEventName);
                    }

                    return false;
                }

                // use null target because event is static
                eventInfo.AddEventHandler(target: null, eventHandler);
                Log.Debug("Subscribed to event {EventName}.", fullEventName);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error adding event handler to {EventName}.", fullEventName);
                return false;
            }
        }

        public static void GetMessageHeaders(EventArgs? eventArgs, out IServiceRemotingRequestEventArgs? requestEventArgs, out IServiceRemotingRequestMessageHeader? messageHeaders)
        {
            requestEventArgs = null;
            messageHeaders = null;

            if (eventArgs == null)
            {
                Log.Warning("Unexpected null EventArgs.");
                return;
            }

            try
            {
                requestEventArgs = eventArgs.DuckAs<IServiceRemotingRequestEventArgs>();
                messageHeaders = requestEventArgs?.Request?.GetHeader();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error accessing request headers.");
                return;
            }

            if (messageHeaders == null)
            {
                Log.Warning("Cannot access request headers.");
            }
        }

        public static Span CreateSpan(
            Tracer tracer,
            ISpanContext? context,
            ServiceRemotingTags tags,
            IServiceRemotingRequestEventArgs? eventArgs,
            IServiceRemotingRequestMessageHeader? messageHeader)
        {
            string? methodName = null;
            string? resourceName = null;
            string? serviceUrl = null;
            string? remotingServiceName = null;

            string serviceFabricServiceName = PlatformHelpers.ServiceFabric.ServiceName;

            if (eventArgs != null)
            {
                methodName = eventArgs.MethodName ??
                             messageHeader?.MethodName ??
                             messageHeader?.MethodId.ToString(CultureInfo.InvariantCulture) ??
                             "unknown_method";

                serviceUrl = eventArgs.ServiceUri?.AbsoluteUri;
                resourceName = serviceUrl == null ? methodName : $"{serviceUrl}/{methodName}";
                remotingServiceName = serviceUrl?.StartsWith("fabric:/") == true ? serviceUrl.Substring(8) : null;
            }

            tags.ApplicationId = PlatformHelpers.ServiceFabric.ApplicationId;
            tags.ApplicationName = PlatformHelpers.ServiceFabric.ApplicationName;
            tags.PartitionId = PlatformHelpers.ServiceFabric.PartitionId;
            tags.NodeId = PlatformHelpers.ServiceFabric.NodeId;
            tags.NodeName = PlatformHelpers.ServiceFabric.NodeName;
            tags.ServiceName = serviceFabricServiceName;
            tags.RemotingUri = serviceUrl;
            tags.RemotingServiceName = remotingServiceName;
            tags.RemotingMethodName = methodName;

            if (messageHeader != null)
            {
                tags.RemotingMethodId = messageHeader.MethodId.ToString(CultureInfo.InvariantCulture);
                tags.RemotingInterfaceId = messageHeader.InterfaceId.ToString(CultureInfo.InvariantCulture);
                tags.RemotingInvocationId = messageHeader.InvocationId;
            }

            Span span = tracer.StartSpan(GetOperationName(tracer, tags.SpanKind), tags, context);
            span.ResourceName = resourceName;
            tags.SetAnalyticsSampleRate(ServiceRemotingConstants.IntegrationId, tracer.Settings, enabledWithGlobalSetting: false);
            tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(ServiceRemotingConstants.IntegrationId);

            return span;
        }

        public static void FinishSpan(EventArgs? e, string spanKind)
        {
            try
            {
                var scope = Tracer.Instance.InternalActiveScope;

                if (scope == null)
                {
                    Log.Warning("Expected an active scope, but there is none.");
                    return;
                }

                string expectedSpanName = GetOperationName(Tracer.Instance, spanKind);

                if (expectedSpanName != scope.Span.OperationName)
                {
                    Log.Warning("Expected span name {ExpectedSpanName}, but found {ActualSpanName} instead.", expectedSpanName, scope.Span.OperationName);
                    return;
                }

                try
                {
                    var eventArgs = e?.DuckAs<IServiceRemotingFailedResponseEventArgs>();
                    var exception = eventArgs?.Error;

                    if (exception != null)
                    {
                        scope.Span?.SetException(exception);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error setting exception tags on span.");
                }

                scope.Dispose();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error accessing or finishing active span.");
            }
        }

        internal static string GetOperationName(Tracer tracer, string spanKind)
        {
#if NET6_0_OR_GREATER
            var requestType = string.Create(null, stackalloc char[128], $"{SpanNamePrefix}.{spanKind}");
#else
            var requestType = $"{SpanNamePrefix}.{spanKind}";
#endif

            if (tracer.CurrentTraceSettings.Schema.Version == SchemaVersion.V0)
            {
                return requestType;
            }

            return spanKind switch
            {
                SpanKinds.Client => tracer.CurrentTraceSettings.Schema.Client.GetOperationNameForRequestType(requestType),
                SpanKinds.Server => tracer.CurrentTraceSettings.Schema.Server.GetOperationNameForRequestType(requestType),
                _ => requestType,
            };
        }
    }
}
