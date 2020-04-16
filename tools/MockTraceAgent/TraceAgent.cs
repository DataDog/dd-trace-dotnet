using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using MessagePack;

namespace MockTraceAgent
{
    public class TraceAgent : IDisposable
    {
        private HttpListener _listener;
        private Thread _listenerThread;

        public void Start(int port)
        {
            // seems like we can't reuse a listener if it fails to start,
            // so create a new listener each time we retry
            var listener = new HttpListener();
            listener.Prefixes.Add($"http://localhost:{port}/");

            listener.Start();

            // successfully listening
            _listener = listener;

            _listenerThread = new Thread(HandleHttpRequests);
            _listenerThread.Start();
        }

        public void Stop()
        {
            _listener.Close();
            _listener = null;
        }

        public event EventHandler<EventArgs<HttpListenerContext>> RequestReceived;

        public event EventHandler<EventArgs<IList<IList<MockSpan>>>> RequestDeserialized;

        private void HandleHttpRequests()
        {
            while (_listener.IsListening)
            {
                try
                {
                    var context = _listener.GetContext();

                    var requestReceivedHandler = RequestReceived;

                    if (requestReceivedHandler != null)
                    {
                        requestReceivedHandler(this, new EventArgs<HttpListenerContext>(context));
                    }

                    var onRequestDeserializedHandler = RequestDeserialized;

                    if (onRequestDeserializedHandler != null)
                    {
                        var traces = MessagePackSerializer.Deserialize<IList<IList<MockSpan>>>(context.Request.InputStream);
                        onRequestDeserializedHandler(this, new EventArgs<IList<IList<MockSpan>>>(traces));
                    }

                    context.Response.ContentType = "application/json";
                    var buffer = Encoding.UTF8.GetBytes("{}");
                    context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                    context.Response.Close();
                }
                catch (HttpListenerException)
                {
                    // listener was stopped
                    return;
                }
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                ((IDisposable)_listener)?.Dispose();
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
