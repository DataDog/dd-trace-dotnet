using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Dispatcher;

namespace Samples.Wcf.Server
{
    internal class DispatchMessageInspector : IDispatchMessageInspector
    {
        public object AfterReceiveRequest(ref Message request, IClientChannel channel, InstanceContext instanceContext)
        {
            var scope = SampleHelpers.GetActiveScope();
            SampleHelpers.TrySetTag(scope, "custom-tag", nameof(DispatchMessageInspector));

            LoggingHelper.WriteLineWithDate($"[Server] AfterReceiveRequest | ActiveScope = {scope}");

            return default;
        }

        public void BeforeSendReply(ref Message reply, object correlationState)
        {
        }
    }
}
