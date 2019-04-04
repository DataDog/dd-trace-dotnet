#if !NETSTANDARD2_0

using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Dispatcher;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    /// <inheritdoc />
    /// <summary>
    ///     IDispatchMessageInspector used to trace within a WCF request
    /// </summary>
    public class WcfDispatchMessageInspector : IDispatchMessageInspector
    {
        /// <inheritdoc />
        public object AfterReceiveRequest(ref Message request, IClientChannel channel, InstanceContext instanceContext)
        {
            var y = "test";
            return y;
        }

        /// <inheritdoc />
        public void BeforeSendReply(ref Message reply, object correlationState)
        {
            var y = correlationState;
        }
    }
}

#endif

