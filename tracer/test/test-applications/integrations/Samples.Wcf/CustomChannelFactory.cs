using System;
using System.ServiceModel;
using System.ServiceModel.Channels;

namespace Samples.Wcf
{
    public class CustomChannelFactory<TChannel> : ChannelFactoryBase<TChannel>
    {
        private readonly IChannelFactory<TChannel> _innerFactory;
        private readonly CustomBindingElement _element;

        public CustomChannelFactory(IChannelFactory<TChannel> innerFactory, CustomBindingElement element)
        {
            _innerFactory = innerFactory;
            _element = element;
        }

        private TChannel WrapChannel(TChannel channel) => channel switch
        {
            IRequestChannel requestChannel => (TChannel)(IRequestChannel)new CustomRequestChannel(requestChannel, _element),
            _ => throw new NotSupportedException("The current session type is not supported"),
        };

        protected override TChannel OnCreateChannel(EndpointAddress address, Uri via)
        {
            var channel = _innerFactory.CreateChannel(address, via);
            return channel is null ? channel : WrapChannel(channel);
        }
            
        protected override void OnOpen(TimeSpan timeout)
            => _innerFactory.Open(timeout);

        protected override IAsyncResult OnBeginOpen(TimeSpan timeout, AsyncCallback callback, object state)
            => _innerFactory.BeginOpen(timeout, callback, state);

        protected override void OnEndOpen(IAsyncResult result)
            => _innerFactory.EndOpen(result);

        protected override void OnClose(TimeSpan timeout)
            => _innerFactory.Close(timeout);

        protected override IAsyncResult OnBeginClose(TimeSpan timeout, AsyncCallback callback, object state)
            => _innerFactory.BeginClose(timeout, callback, state);

        protected override void OnEndClose(IAsyncResult result)
            => _innerFactory.EndClose(result);

        protected override void OnAbort()
            => _innerFactory.Abort();

        public override T GetProperty<T>()
            => _innerFactory.GetProperty<T>();
    }
}
