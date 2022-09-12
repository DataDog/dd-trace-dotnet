using System;
using System.Net;
using System.Threading.Tasks;

namespace Samples.Probes.Shared
{
    public class SimpleHttpListener : IDisposable
    {
        private readonly HttpListener _listener;
        private readonly Func<Task> _task;

        public SimpleHttpListener(string listenUri, Func<Task> task)
        {
            _task = task;
            _listener = new HttpListener();
            _listener.Prefixes.Add(listenUri);
        }

        public async Task HandleIncomingConnections()
        {
            _listener.Start();

            var run = true;

            while (run)
            {
                var ctx = await _listener.GetContextAsync();


                Console.WriteLine($"[{ctx.Request.HttpMethod}] {ctx.Request.Url}");
                var absolutePath = ctx.Request.Url.AbsolutePath;

                switch (absolutePath)
                {
                    case "/stop":
                        Console.WriteLine("Stop requested");
                        run = false;
                        break;
                    default:
                        if (_task != null)
                        {
                            await _task.Invoke();
                        }

                        break;
                }

                ctx.Response.Close();
            }
        }

        public void Dispose()
        {
            _listener.Close();
        }
    }
}
