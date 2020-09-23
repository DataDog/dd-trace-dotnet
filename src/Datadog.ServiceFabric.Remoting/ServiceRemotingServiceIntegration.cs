using System;
using Microsoft.ServiceFabric.Services.Remoting;
using Microsoft.ServiceFabric.Services.Remoting.V2;
using Microsoft.ServiceFabric.Services.Remoting.V2.Client;
using Microsoft.ServiceFabric.Services.Remoting.V2.Runtime;

namespace Datadog.Trace.ServiceFabric.Remoting
{
    // https: //github.com/microsoft/ApplicationInsights-ServiceFabric/blob/master/src/ApplicationInsights.ServiceFabric.Native.Shared/RequestTrackingModule/ServiceRemotingServerEventListener.cs
    public class ServiceRemotingServiceIntegration
    {
        public void Initialize()
        {
            ServiceRemotingServiceEvents.ReceiveRequest += ServiceRemotingServiceEvents_ReceiveRequest;
            ServiceRemotingServiceEvents.SendResponse += ServiceRemotingServiceEvents_SendResponse;
        }

        private void ServiceRemotingServiceEvents_ReceiveRequest(object sender, EventArgs e)
        {
            ServiceRemotingRequestEventArgs eventArgs = e as ServiceRemotingRequestEventArgs;

            if (eventArgs == null)
            {
                // ServiceFabricSDKEventSource.Log.InvalidEventArgument((typeof(ServiceRemotingRequestEventArgs)).Name, e.GetType().Name);
                return;
            }

            var request = eventArgs.Request;
            var messageHeaders = request?.GetHeader();

            // If there are no header objects passed in, we don't do anything.
            if (messageHeaders == null)
            {
                // ServiceFabricSDKEventSource.Log.HeadersNotFound();
                return;
            }

            string methodName = eventArgs.MethodName;

            // TODO: use "service" in operation name, or just rely on span.type?
            string operationName = "servicefabric.service";
            string serviceName = "";

            messageHeaders.TryGetHeaderValue(HttpHeaderNames.TraceId, out byte[] traceIdBytes);
            messageHeaders.TryGetHeaderValue(HttpHeaderNames.ParentId, out byte[] parentIdBytes);
            messageHeaders.TryGetHeaderValue(HttpHeaderNames.SamplingPriority, out byte[] samplingPriorityBytes);

            ulong traceId = BitConverter.ToUInt64(traceIdBytes, 0);
            ulong parentId = BitConverter.ToUInt64(parentIdBytes, 0);
            var priority = (SamplingPriority)BitConverter.ToInt32(samplingPriorityBytes, 0);
            var context = new SpanContext(traceId, parentId, priority);

            var tracer = Tracer.Instance;
            Span span = tracer.StartSpan(operationName, context, serviceName);

            //....

            Scope scope = tracer.ActivateSpan(span);
        }

        private void ServiceRemotingServiceEvents_SendResponse(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }
    }
}
