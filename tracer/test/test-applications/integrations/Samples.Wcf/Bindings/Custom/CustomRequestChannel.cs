using System;
using System.ServiceModel;
using System.ServiceModel.Channels;

namespace Samples.Wcf.Bindings.Custom
{
    public class CustomRequestChannel : CustomChannelBase, IRequestChannel
    {
        private readonly IRequestChannel _innerChannel;

        public CustomRequestChannel(IRequestChannel innerChannel, CustomBindingElement element)
            : base(innerChannel, element)
        {
            _innerChannel = innerChannel;
        }

        public EndpointAddress RemoteAddress => _innerChannel.RemoteAddress;

        public Uri Via => _innerChannel.Via;

        public IAsyncResult BeginRequest(Message message, AsyncCallback callback, object state)
            => _innerChannel.BeginRequest(message, callback, state);

        public IAsyncResult BeginRequest(Message message, TimeSpan timeout, AsyncCallback callback, object state)
            => _innerChannel.BeginRequest(message, timeout, callback, state);

        public Message EndRequest(IAsyncResult result)
            => _innerChannel.EndRequest(result);

        public Message Request(Message message)
            => _innerChannel.Request(message);

        public Message Request(Message message, TimeSpan timeout)
            => _innerChannel.Request(message, timeout);
    }
}
