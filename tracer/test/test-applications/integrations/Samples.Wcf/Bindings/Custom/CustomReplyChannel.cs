using System;
using System.ServiceModel;
using System.ServiceModel.Channels;

namespace Samples.Wcf.Bindings.Custom
{
    public class CustomReplyChannel : CustomChannelBase, IReplyChannel
    {
        private readonly IReplyChannel _innerChannel;

        public CustomReplyChannel(IReplyChannel innerChannel, CustomBindingElement element)
            : base(innerChannel, element)
        {
            _innerChannel = innerChannel;
        }

        private RequestContext WrapRequestContext(RequestContext context)
            => new CustomReplyChannelRequestContext(context);

        //// Begin IReplyChannel interface methods

        public EndpointAddress LocalAddress => _innerChannel.LocalAddress;

        public IAsyncResult BeginReceiveRequest(AsyncCallback callback, object state)
            => _innerChannel.BeginReceiveRequest(callback, state);

        public IAsyncResult BeginReceiveRequest(TimeSpan timeout, AsyncCallback callback, object state)
            => _innerChannel.BeginReceiveRequest(timeout, callback, state);

        public IAsyncResult BeginTryReceiveRequest(TimeSpan timeout, AsyncCallback callback, object state)
            => _innerChannel.BeginTryReceiveRequest(timeout, callback, state);

        public IAsyncResult BeginWaitForRequest(TimeSpan timeout, AsyncCallback callback, object state)
            => _innerChannel.BeginWaitForRequest(timeout, callback, state);

        public RequestContext EndReceiveRequest(IAsyncResult result)
            => WrapRequestContext(_innerChannel.EndReceiveRequest(result));

        public bool EndTryReceiveRequest(IAsyncResult result, out RequestContext context)
        {
            var retVal = _innerChannel.EndTryReceiveRequest(result, out context);
            if (retVal && context is not null)
            {
                context = WrapRequestContext(context);
            }
            return retVal;
        }

        public bool EndWaitForRequest(IAsyncResult result)
            => _innerChannel.EndWaitForRequest(result);

        public RequestContext ReceiveRequest()
            => WrapRequestContext(_innerChannel.ReceiveRequest());

        public RequestContext ReceiveRequest(TimeSpan timeout)
            => WrapRequestContext(_innerChannel.ReceiveRequest(timeout));

        public bool TryReceiveRequest(TimeSpan timeout, out RequestContext context)
        {
            var retVal = _innerChannel.TryReceiveRequest(timeout, out context);
            if (retVal && context is not null)
            {
                context = WrapRequestContext(context);
            }
            return retVal;
        }

        public bool WaitForRequest(TimeSpan timeout)
            => _innerChannel.WaitForRequest(timeout);

        //// End IReplyChannel interface methods
    }
}
