using System;
using System.ServiceModel.Channels;

namespace Samples.Wcf
{
    public class CustomChannelListener<TChannel> : ChannelListenerBase<TChannel> where TChannel : class, IChannel
    {
        private readonly IChannelListener<TChannel> _innerListener;
        private readonly CustomBindingElement _element;

        public CustomChannelListener(IChannelListener<TChannel> innerListener, CustomBindingElement element)
        {
            _innerListener = innerListener;
            _element = element;
        }

        public override Uri Uri => _innerListener.Uri;

        private TChannel WrapChannel(TChannel channel) => channel switch
        {
            IReplyChannel replyChannel => (TChannel)(IReplyChannel)new CustomReplyChannel(replyChannel, _element),
            _ => throw new NotSupportedException("The current session type is not supported"),
        };

        protected override TChannel OnAcceptChannel(TimeSpan timeout)
        {
            var channel = _innerListener.AcceptChannel(timeout);
            return channel is null ? channel : WrapChannel(channel);
        }

        protected override IAsyncResult OnBeginAcceptChannel(TimeSpan timeout, AsyncCallback callback, object state)
            => _innerListener.BeginAcceptChannel(timeout, callback, state);

        protected override TChannel OnEndAcceptChannel(IAsyncResult result)
        {
            var channel = _innerListener.EndAcceptChannel(result);
            return channel is null ? channel : WrapChannel(channel);
        }

        protected override void OnOpen(TimeSpan timeout)
            => _innerListener.Open(timeout);

        protected override IAsyncResult OnBeginOpen(TimeSpan timeout, AsyncCallback callback, object state)
            => _innerListener.BeginOpen(timeout, callback, state);

        protected override void OnEndOpen(IAsyncResult result)
            => _innerListener.EndOpen(result);

        protected override void OnClose(TimeSpan timeout)
            => _innerListener.Close(timeout);

        protected override IAsyncResult OnBeginClose(TimeSpan timeout, AsyncCallback callback, object state)
            => _innerListener.BeginClose(timeout, callback, state);

        protected override void OnEndClose(IAsyncResult result)
            => _innerListener.EndClose(result);

        protected override bool OnWaitForChannel(TimeSpan timeout)
            => _innerListener.WaitForChannel(timeout);

        protected override IAsyncResult OnBeginWaitForChannel(TimeSpan timeout, AsyncCallback callback, object state)
            => _innerListener.BeginWaitForChannel(timeout, callback, state);

        protected override bool OnEndWaitForChannel(IAsyncResult result)
            => _innerListener.EndWaitForChannel(result);

        protected override void OnAbort()
            => _innerListener.Abort();

        public override T GetProperty<T>()
            => _innerListener.GetProperty<T>();
    }
}
