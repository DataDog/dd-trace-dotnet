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
        public const string AssemblyName = "Microsoft.ServiceFabric.Services.Remoting";

        public const string ClientEventsTypeName = "Microsoft.ServiceFabric.Services.Remoting.V2.Client.ServiceRemotingClientEvents";

        public const string ServiceEventsTypeName = "Microsoft.ServiceFabric.Services.Remoting.V2.Runtime.ServiceRemotingServiceEvents";

        public const string SpanNamePrefix = "service-remoting";

        public static readonly IntegrationInfo IntegrationId = IntegrationRegistry.GetIntegrationInfo(nameof(IntegrationIds.ServiceRemoting));

        private static readonly Logging.IDatadogLogger Log = Logging.DatadogLogging.GetLoggerFor(typeof(ServiceRemotingHelpers));

        public static bool AddEventHandler(string typeName, string eventName, EventHandler eventHandler)
        {
            try
            {
                Type? type = Type.GetType(typeName, throwOnError: false);

                if (type == null)
                {
                    Log.Warning("Could not get type {typeName} via reflection.", typeName);
                    return false;
                }

                EventInfo? eventInfo = type.GetEvent(eventName, BindingFlags.Static | BindingFlags.Public);

                if (eventInfo == null)
                {
                    Log.Warning("Could not get event {typeName}.{eventName} via reflection.", typeName, eventName);
                    return false;
                }

                // use null target because event is static
                eventInfo.AddEventHandler(target: null, eventHandler);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error adding event handler to {typeName}.{eventName}.", typeName, eventName);
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

            string? eventArgsTypeName = eventArgs.GetType().FullName;

            if (eventArgsTypeName != "Microsoft.ServiceFabric.Services.Remoting.V2.ServiceRemotingRequestEventArgs")
            {
                Log.Warning("Unexpected eventArgs type: {type}.", eventArgsTypeName ?? "null");
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

            if (eventArgs != null)
            {
                methodName = eventArgs.MethodName ??
                             messageHeader?.MethodName ??
                             messageHeader?.MethodId.ToString(CultureInfo.InvariantCulture) ??
                             "unknown_method";

                serviceUrl = eventArgs.ServiceUri?.AbsoluteUri ?? "unknown_url";
                resourceName = $"{serviceUrl}/{methodName}";
            }

            var tags = new ServiceRemotingTags(spanKind)
                       {
                           Uri = serviceUrl,
                           MethodName = methodName
                       };

            if (messageHeader != null)
            {
                tags.MethodId = messageHeader.MethodId.ToString(CultureInfo.InvariantCulture);
                tags.InterfaceId = messageHeader.InterfaceId.ToString(CultureInfo.InvariantCulture);
                tags.InvocationId = messageHeader.InvocationId;
            }

            Span span = tracer.StartSpan(ServiceRemotingHelpers.GetSpanName(spanKind), tags, context);
            span.ResourceName = resourceName ?? "unknown";

            switch (spanKind)
            {
                case SpanKinds.Client:
                    tags.SetAnalyticsSampleRate(IntegrationId, Tracer.Instance.Settings, enabledWithGlobalSetting: false);
                    break;
                case SpanKinds.Server:
                    tags.SetAnalyticsSampleRate(IntegrationId, Tracer.Instance.Settings, enabledWithGlobalSetting: true);
                    break;
            }

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
