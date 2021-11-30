using System;
using System.ServiceModel;
using System.ServiceModel.Channels;

namespace Samples.Wcf.Bindings.Custom
{
    public class CustomChannelBase : IChannel, ICommunicationObject
    {
        private readonly IChannel _innerChannel;
        private readonly CustomBindingElement _element;

        protected CustomChannelBase(IChannel innerChannel, CustomBindingElement element)
        {
            _innerChannel = innerChannel;
            _element = element;
        }

        //// Begin ICommunicationObject interface methods

        public CommunicationState State => _innerChannel.State;

#pragma warning disable CS0067
        public event EventHandler Closed;
        public event EventHandler Closing;
        public event EventHandler Faulted;
        public event EventHandler Opened;
        public event EventHandler Opening;
#pragma warning restore CS0067

        public void Abort()
            => _innerChannel.Abort();

        public void Open()
            => _innerChannel.Open();

        public void Open(TimeSpan timeout)
            => _innerChannel.Open(timeout);

        public IAsyncResult BeginOpen(AsyncCallback callback, object state)
            => _innerChannel.BeginOpen(callback, state);

        public IAsyncResult BeginOpen(TimeSpan timeout, AsyncCallback callback, object state)
            => _innerChannel.BeginOpen(timeout, callback, state);

        public void EndOpen(IAsyncResult result)
            => _innerChannel.EndOpen(result);

        public void Close()
            => _innerChannel.Close();

        public void Close(TimeSpan timeout)
            => _innerChannel.Close(timeout);

        public IAsyncResult BeginClose(AsyncCallback callback, object state)
            => _innerChannel.BeginClose(callback, state);

        public IAsyncResult BeginClose(TimeSpan timeout, AsyncCallback callback, object state)
            => _innerChannel.BeginClose(timeout, callback, state);

        public void EndClose(IAsyncResult result)
            => _innerChannel.EndClose(result);

        //// End ICommunicationObject interface methods

        //// Begin IChannel interface methods

        public T GetProperty<T>() where T : class
            => _innerChannel.GetProperty<T>();

        //// End IChannel interface methods
    }
}
