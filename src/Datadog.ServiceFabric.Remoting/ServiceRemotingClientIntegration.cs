using System;
using System.Globalization;
using Datadog.Trace.ExtensionMethods;
using Microsoft.ServiceFabric.Services.Remoting;
using Microsoft.ServiceFabric.Services.Remoting.V2;
using Microsoft.ServiceFabric.Services.Remoting.V2.Client;

namespace Datadog.Trace.ServiceFabric.Remoting
{
    // https://github.com/microsoft/ApplicationInsights-ServiceFabric/blob/master/src/ApplicationInsights.ServiceFabric.Native.Shared/DependencyTrackingModule/ServiceRemotingClientEventListener.cs
    public class ServiceRemotingClientIntegration
    {
        public void Initialize()
        {
            ServiceRemotingClientEvents.SendRequest += ServiceRemotingClientEvents_SendRequest;
            ServiceRemotingClientEvents.ReceiveResponse += ServiceRemotingClientEvents_ReceiveResponse;
        }

        private void ServiceRemotingClientEvents_SendRequest(object sender, EventArgs e)
        {
            var eventArgs = e as ServiceRemotingRequestEventArgs;

            if (eventArgs == null)
            {
                // TODO: log invalid event argument and exit
                return;
            }

            var service = (IService)sender;
            var request = eventArgs.Request;
            var messageHeaders = request?.GetHeader();

            if (messageHeaders == null)
            {
                // TODO: log missing headers and exit
                return;
            }

            Uri serviceUri = eventArgs.ServiceUri;
            string methodName = eventArgs.MethodName;

            // Weird case, just use the numerical id as the method name
            if (string.IsNullOrEmpty(methodName))
            {
                methodName = messageHeaders.MethodId.ToString(CultureInfo.InvariantCulture);
            }

            // TODO: use "client" in operation name, or just rely on span.type?
            string operationName = "servicefabric.client";
            string serviceName = "";

            var tracer = Tracer.Instance;
            Span span = tracer.StartSpan(operationName, serviceName: serviceName);
            span.ResourceName = serviceUri.AbsoluteUri + "/" + methodName;
            span.SetTag(Tags.SpanKind, SpanKinds.Client);

            if (!messageHeaders.TryGetHeaderValue(HttpHeaderNames.TraceId, out _) &&
                !messageHeaders.TryGetHeaderValue(HttpHeaderNames.ParentId, out _))
            {
                ulong traceId = tracer.ActiveScope?.Span?.TraceId ?? 0;
                messageHeaders.AddHeader(HttpHeaderNames.TraceId, BitConverter.GetBytes(traceId));

                ulong spanId = tracer.ActiveScope?.Span?.SpanId ?? 0;
                messageHeaders.AddHeader(HttpHeaderNames.ParentId, BitConverter.GetBytes(spanId));

                tracer.ActiveScope?.Span.SetTraceSamplingPriority();

                // TODO: sampling priority

                // TODO: origin
            }

            Scope scope = tracer.ActivateSpan(span);
        }

        private void ServiceRemotingClientEvents_ReceiveResponse(object sender, EventArgs e)
        {
            ServiceRemotingResponseEventArgs successfulResponseArg = e as ServiceRemotingResponseEventArgs;
            ServiceRemotingFailedResponseEventArgs failedResponseArg = e as ServiceRemotingFailedResponseEventArgs;
        }
    }
}
