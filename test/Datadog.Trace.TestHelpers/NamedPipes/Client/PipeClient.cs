using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers.NamedPipes.Interfaces;
using Datadog.Trace.TestHelpers.NamedPipes.Utilities;

namespace Datadog.Trace.TestHelpers.NamedPipes.Client
{
    internal class PipeClient : ICommunicationClient
    {
        private readonly NamedPipeClientStream _pipeClient;

        public PipeClient(string serverId)
        {
            _pipeClient = new NamedPipeClientStream(".", serverId, PipeDirection.InOut, PipeOptions.Asynchronous);
        }

        public void Start()
        {
            const int tryConnectTimeout = 5 * 1000; // 5 seconds
            _pipeClient.Connect(tryConnectTimeout);
        }

        public void Stop()
        {
            try
            {
#pragma warning disable CA1416
                _pipeClient.WaitForPipeDrain();
#pragma warning restore CA1416
            }
            finally
            {
                _pipeClient.Close();
                _pipeClient.Dispose();
            }
        }

        public Task<TaskResult> SendMessage(string message)
        {
            var taskCompletionSource = new TaskCompletionSource<TaskResult>();

            if (_pipeClient.IsConnected)
            {
                var buffer = Encoding.UTF8.GetBytes(message);
                _pipeClient.BeginWrite(
                    buffer,
                    0,
                    buffer.Length,
                    asyncResult =>
                    {
                        try
                        {
                            taskCompletionSource.SetResult(EndWriteCallBack(asyncResult));
                        }
                        catch (Exception ex)
                        {
                            taskCompletionSource.SetException(ex);
                        }
                    },
                    null);
            }
            else
            {
                Logger.Error("Cannot send message, pipe is not connected");
                throw new IOException("pipe is not connected");
            }

            return taskCompletionSource.Task;
        }

        private TaskResult EndWriteCallBack(IAsyncResult asyncResult)
        {
            _pipeClient.EndWrite(asyncResult);
            _pipeClient.Flush();

            return new TaskResult { IsSuccess = true };
        }
    }
}
