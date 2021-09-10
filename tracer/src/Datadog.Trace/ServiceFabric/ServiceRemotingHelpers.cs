// <copyright file="ServiceRemotingHelpers.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Globalization;
using System.Reflection;
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
                    Log.Warning("Could not get type {typeName}.", typeName);
                    return false;
                }

                EventInfo? eventInfo = type.GetEvent(eventName, BindingFlags.Static | BindingFlags.Public);

                if (eventInfo == null)
                {
                    Log.Warning("Could not get event {eventName}.", fullEventName);
                    return false;
                }

                // use null target because event is static
                eventInfo.AddEventHandler(target: null, eventHandler);
                Log.Debug("Subscribed to event {eventName}.", fullEventName);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error adding event handler to {eventName}.", fullEventName);
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

        public static string GetSpanName(string spanKind)
        {
            return $"{SpanNamePrefix}.{spanKind}";
        }

        public static Span CreateSpan(
            Tracer tracer,
            ISpanContext? context,
            string spanKind,
            IServiceRemotingRequestEventArgs? eventArgs,
            IServiceRemotingRequestMessageHeader? messageHeader)
        {
            string? methodName = null;
            string? resourceName = null;
            string? serviceUrl = null;

            string serviceFabricServiceName = PlatformHelpers.ServiceFabric.ServiceName;

            if (eventArgs != null)
            {
                methodName = eventArgs.MethodName ??
                             messageHeader?.MethodName ??
                             messageHeader?.MethodId.ToString(CultureInfo.InvariantCulture) ??
                             "unknown_method";

                serviceUrl = eventArgs.ServiceUri?.AbsoluteUri;
                resourceName = serviceUrl == null ? methodName : $"{serviceUrl}/{methodName}";
            }

            var tags = new ServiceRemotingTags(spanKind)
            {
                ApplicationId = PlatformHelpers.ServiceFabric.ApplicationId,
                ApplicationName = PlatformHelpers.ServiceFabric.ApplicationName,
                PartitionId = PlatformHelpers.ServiceFabric.PartitionId,
                NodeId = PlatformHelpers.ServiceFabric.NodeId,
                NodeName = PlatformHelpers.ServiceFabric.NodeName,
                ServiceName = serviceFabricServiceName,
                RemotingUri = serviceUrl,
                RemotingMethodName = methodName
            };

            if (messageHeader != null)
            {
                tags.RemotingMethodId = messageHeader.MethodId.ToString(CultureInfo.InvariantCulture);
                tags.RemotingInterfaceId = messageHeader.InterfaceId.ToString(CultureInfo.InvariantCulture);
                tags.RemotingInvocationId = messageHeader.InvocationId;
            }

            Span span = tracer.StartSpan(GetSpanName(spanKind), tags, context);
            span.ResourceName = resourceName;
            tags.SetAnalyticsSampleRate(ServiceRemotingConstants.IntegrationId, Tracer.Instance.Settings, enabledWithGlobalSetting: false);

            return span;
        }

        public static void FinishSpan(EventArgs? e, string spanKind)
        {
            try
            {
                var scope = Tracer.Instance.ActiveScope;

                if (scope == null)
                {
                    Log.Warning("Expected an active scope, but there is none.");
                    return;
                }

                string expectedSpanName = GetSpanName(spanKind);

                if (expectedSpanName != scope.Span.OperationName)
                {
                    Log.Warning("Expected span name {expectedSpanName}, but found {actualSpanName} instead.", expectedSpanName, scope.Span.OperationName);
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
    }
}
