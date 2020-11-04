using System;
using System.IO;
using System.IO.Pipes;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent.MessagePack;

namespace Datadog.Trace.Agent.NamedPipes
{
    internal class NamedPipesApiRequest : IApiRequest
    {
        private readonly NamedPipeClientStream _namedPipe;
        private readonly TimeSpan _timeout;
        private byte[] _internalbuffer = new byte[0];
        private readonly HttpMessageContent _httpMessageContent;

        // `SpinLock` is a struct. A struct marked as `readonly` is copied each time a mutating function is called.
        // When calling `_lock.Enter` and `_lock.Exit()` the `SpinLock` instance is copied. Calling `_lock.Exit()` raises an
        // error as the instance does not hold the lock (System.Threading.SynchronizationLockException : The calling
        // thread does not hold the lock.)
        // For this reason, `_lock` is not marked as `readonly`
        private SpinLock _lock = new SpinLock(enableThreadOwnerTracking: true);

        public NamedPipesApiRequest(string pipeName)
        {
            _namedPipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            _timeout = TimeSpan.FromSeconds(2);
            _httpMessageContent = new NamedPipeHttpMessageContent(new HttpRequestMessage(HttpMethod.Post, pipeName));
        }

        public void AddHeader(string name, string value)
        {
            _httpMessageContent.Headers.Add(name, value);
        }

        public async Task<IApiResponse> PostAsync(Span[][] traces, FormatterResolverWrapper formatterResolver)
        {
            using (var content = new NamedPipeHttpMessageContent(traces, formatterResolver))
            {
                _httpMessageContent.HttpRequestMessage.Content = content;

                var response = await _client.(_request).ConfigureAwait(false);

                return new HttpClientResponse(response);
            }
        }

        public bool Send(byte[] buffer, int length)
        {
            var gotLock = false;
            try
            {
                _lock.Enter(ref gotLock);
                if (_internalbuffer.Length < length + 1)
                {
                    _internalbuffer = new byte[length + 1];
                }

                // Server expects messages to end with '\n'
                Array.Copy(buffer, 0, _internalbuffer, 0, length);
                _internalbuffer[length] = (byte)'\n';

                return SendBuffer(_internalbuffer, length + 1, allowRetry: true);
            }
            finally
            {
                if (gotLock)
                {
                    _lock.Exit();
                }
            }
        }

        public void Dispose()
        {
            _namedPipe.Dispose();
        }

        private bool SendBuffer(byte[] buffer, int length, bool allowRetry)
        {
            try
            {
                if (!_namedPipe.IsConnected)
                {
                    _namedPipe.Connect((int)_timeout.TotalMilliseconds);
                }
            }
            catch (TimeoutException)
            {
                return false;
            }

            var cts = new CancellationTokenSource(_timeout);

            try
            {
                // WriteAsync overload with a CancellationToken instance seems to not work.
                _namedPipe.WriteAsync(buffer, 0, length).Wait(cts.Token);
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (IOException)
            {
                // When the server disconnects, IOException is raised with the message "Pipe is broken".
                // In this case, we try to reconnect once.
                if (allowRetry)
                {
                    return SendBuffer(buffer, length, allowRetry: false);
                }

                return false;
            }
        }
    }
}
