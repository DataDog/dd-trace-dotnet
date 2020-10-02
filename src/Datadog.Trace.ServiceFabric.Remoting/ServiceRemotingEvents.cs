using System;
using System.Globalization;
using System.Text;
using Microsoft.ServiceFabric.Services.Remoting.V2;
using Microsoft.ServiceFabric.Services.Remoting.V2.Client;
using Microsoft.ServiceFabric.Services.Remoting.V2.Runtime;

/*
https://github.com/microsoft/ApplicationInsights-ServiceFabric/blob/master/src/ApplicationInsights.ServiceFabric.Native.Shared/DependencyTrackingModule/ServiceRemotingClientEventListener.cs
https://github.com/microsoft/ApplicationInsights-ServiceFabric/blob/master/src/ApplicationInsights.ServiceFabric.Native.Shared/RequestTrackingModule/ServiceRemotingServerEventListener.cs
*/

namespace Datadog.Trace
{
    public static class ServiceRemotingEvents
    {
        public const string IntegrationName = "ServiceRemoting";

        public static void StartTracing()
        {
            // client
            ServiceRemotingClientEvents.SendRequest += ServiceRemotingClientEvents_SendRequest;
            ServiceRemotingClientEvents.ReceiveResponse += ServiceRemotingClientEvents_ReceiveResponse;


            // server
            ServiceRemotingServiceEvents.ReceiveRequest += ServiceRemotingServiceEvents_ReceiveRequest;
            ServiceRemotingServiceEvents.SendResponse += ServiceRemotingServiceEvents_SendResponse;
        }

        public static void StopTracing()
        {
            // client
            ServiceRemotingClientEvents.SendRequest -= ServiceRemotingClientEvents_SendRequest;
            ServiceRemotingClientEvents.ReceiveResponse -= ServiceRemotingClientEvents_ReceiveResponse;


            // server
            ServiceRemotingServiceEvents.ReceiveRequest -= ServiceRemotingServiceEvents_ReceiveRequest;
            ServiceRemotingServiceEvents.SendResponse -= ServiceRemotingServiceEvents_SendResponse;
        }

        private static void ServiceRemotingClientEvents_SendRequest(object? sender, EventArgs e)
        {
            var eventArgs = e as ServiceRemotingRequestEventArgs;

            if (eventArgs == null)
            {
                // TODO: log
            }

            var messageHeaders = eventArgs?.Request?.GetHeader();

            if (messageHeaders == null)
            {
                // TODO: log
            }

            var tracer = Tracer.Instance;
            var span = CreateSpan(tracer, context: null, SpanKinds.Client, eventArgs, messageHeaders);

            // inject trace propagation headers for distributed tracing
            if (messageHeaders != null)
            {
                if (!messageHeaders.TryGetHeaderValue(HttpHeaderNames.TraceId, out _))
                {
                    messageHeaders.AddHeader(HttpHeaderNames.TraceId, BitConverter.GetBytes(span.TraceId));
                }

                if (!messageHeaders.TryGetHeaderValue(HttpHeaderNames.ParentId, out _))
                {
                    messageHeaders.AddHeader(HttpHeaderNames.ParentId, BitConverter.GetBytes(span.SpanId));
                }

                if (!messageHeaders.TryGetHeaderValue(HttpHeaderNames.SamplingPriority, out _) &&
                    ulong.TryParse(span.GetTag(Tags.SamplingPriority), out ulong samplingPriority))
                {
                    messageHeaders.AddHeader(HttpHeaderNames.SamplingPriority, BitConverter.GetBytes(samplingPriority));
                }

                if (!messageHeaders.TryGetHeaderValue(HttpHeaderNames.Origin, out _))
                {
                    string origin = span.GetTag(Tags.Origin);
                    messageHeaders.AddHeader(HttpHeaderNames.Origin, Encoding.UTF8.GetBytes(origin));
                }
            }

            tracer.ActivateSpan(span);
        }

        private static void ServiceRemotingClientEvents_ReceiveResponse(object? sender, EventArgs e)
        {
            // var successfulResponseArg = e as ServiceRemotingResponseEventArgs;
            // var failedResponseArg = e as ServiceRemotingFailedResponseEventArgs;

            var scope = Tracer.Instance.ActiveScope;

            if (scope != null)
            {
                if (e is ServiceRemotingFailedResponseEventArgs failedResponseArg && failedResponseArg.Error != null)
                {
                    scope.Span?.SetException(failedResponseArg.Error);
                }

                scope.Dispose();
            }
        }

        private static void ServiceRemotingServiceEvents_ReceiveRequest(object? sender, EventArgs e)
        {
            var eventArgs = e as ServiceRemotingRequestEventArgs;

            if (eventArgs == null)
            {
                // TODO: log
            }

            var messageHeaders = eventArgs?.Request?.GetHeader();
            SpanContext? context = null;
            string? origin = null;

            if (messageHeaders == null)
            {
                // TODO: log
            }
            else
            {
                // extract trace propagation headers for distributed tracing
                ulong? traceId = null;
                ulong? parentId = null;
                SamplingPriority? samplingPriority = null;

                if (messageHeaders.TryGetHeaderValue(HttpHeaderNames.TraceId, out byte[] traceIdBytes))
                {
                    traceId = BitConverter.ToUInt64(traceIdBytes, 0);
                }

                if (messageHeaders.TryGetHeaderValue(HttpHeaderNames.ParentId, out byte[] parentIdBytes))
                {
                    parentId = BitConverter.ToUInt64(parentIdBytes, 0);
                }

                if (messageHeaders.TryGetHeaderValue(HttpHeaderNames.SamplingPriority, out byte[] samplingPriorityBytes))
                {
                    samplingPriority = (SamplingPriority)BitConverter.ToInt32(samplingPriorityBytes, 0);
                }

                if (messageHeaders.TryGetHeaderValue(HttpHeaderNames.Origin, out byte[] originBytes))
                {
                    origin = Encoding.UTF8.GetString(originBytes);
                }

                if (traceId != null && parentId != null)
                {
                    context = new SpanContext(traceId.Value, parentId.Value, samplingPriority);
                }
            }

            var tracer = Tracer.Instance;
            var span = CreateSpan(tracer, context, SpanKinds.Client, eventArgs, messageHeaders);
            span.SetTag(Tags.Origin, origin);

            tracer.ActivateSpan(span);
        }

        private static void ServiceRemotingServiceEvents_SendResponse(object? sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

        private static Span CreateSpan(
            Tracer tracer,
            SpanContext? context,
            string spanKind,
            ServiceRemotingRequestEventArgs? eventArgs,
            IServiceRemotingRequestMessageHeader? messageHeader)
        {
            string? resourceName = null;

            if (eventArgs != null)
            {
                Uri serviceUri = eventArgs.ServiceUri;
                string methodName = eventArgs.MethodName;

                if (string.IsNullOrEmpty(methodName))
                {
                    // use the numeric id as the method name
                    methodName = messageHeader == null ? "unknown" : messageHeader.MethodId.ToString(CultureInfo.InvariantCulture);
                }

                resourceName = $"{serviceUri.AbsoluteUri}/{methodName}";
            }

            Span span = tracer.StartSpan($"servicefabric.{spanKind}", context);
            span.ResourceName = resourceName ?? "unknown";
            span.SetTag(Tags.SpanKind, spanKind);

            if (eventArgs != null)
            {
                span.SetTag(Tags.HttpUrl, eventArgs.ServiceUri.AbsoluteUri);
                span.SetTag("method-name", eventArgs.MethodName);
            }

            if (messageHeader != null)
            {
                span.SetTag("method-id", messageHeader.MethodId.ToString(CultureInfo.InvariantCulture));
                span.SetTag("interface-id", messageHeader.InterfaceId.ToString(CultureInfo.InvariantCulture));
                span.SetTag("invocation-id", messageHeader.InvocationId);
            }

            double? analyticsSampleRate = GetAnalyticsSampleRate(tracer, enabledWithGlobalSetting: false);

            if (analyticsSampleRate != null)
            {
                span.SetTag(Tags.Analytics, analyticsSampleRate.Value.ToString(CultureInfo.InvariantCulture));
            }

            return span;
        }

        private static double? GetAnalyticsSampleRate(Tracer tracer, bool enabledWithGlobalSetting)
        {
            var integrationSettings = tracer.Settings.Integrations[IntegrationName];
            var analyticsEnabled = integrationSettings.AnalyticsEnabled ?? (enabledWithGlobalSetting && tracer.Settings.AnalyticsEnabled);
            return analyticsEnabled ? integrationSettings.AnalyticsSampleRate : (double?)null;
        }
    }
}
