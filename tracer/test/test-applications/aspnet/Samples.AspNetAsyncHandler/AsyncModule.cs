using System;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace Samples.AspNetAsyncHandler
{
    public class AsyncModule : IHttpModule
    {
        static AsyncModule()
        {
            const int nbThreads = 2;

            ThreadPool.SetMinThreads(nbThreads, 2);
            ThreadPool.SetMaxThreads(nbThreads, 1000);
        }

        public void Init(HttpApplication app)
        {
            app.AddOnAuthenticateRequestAsync(BeginEvent, EndEvent, app);
            app.BeginRequest += App_BeginRequest;
            app.EndRequest += App_EndRequest;
        }

        private void App_BeginRequest(object sender, EventArgs e)
        {
            HttpContext.Current.Items.Add(this, new ManualResetEventSlim());
        }

        private void App_EndRequest(object sender, EventArgs e)
        {
            var mutex = (ManualResetEventSlim)HttpContext.Current.Items[this];
            mutex.Set();
        }

        public void Dispose()
        {
        }

        private IAsyncResult BeginEvent(object sender, EventArgs e, AsyncCallback cb, object extraData)
        {
            var app = (HttpApplication)extraData;

            var asyncResult = new AsyncResult();

            ThreadPool.UnsafeQueueUserWorkItem(
                _ =>
                {
                    var mutex = new ManualResetEventSlim(false);

                    Task.Run(
                        () =>
                        {
                            mutex.Set();

                            CaptureThread(app);

                            asyncResult.IsCompleted = true;
                            asyncResult.WaitHandle.Set();

                            cb(asyncResult);
                        });

                    mutex.Wait();
                }, null);

            return asyncResult;
        }

        private void CaptureThread(HttpApplication app)
        {
            var mutex = new ManualResetEventSlim();

            ThreadPool.UnsafeQueueUserWorkItem(_ =>
            {
                mutex.Set();

                var requestMutex = (ManualResetEventSlim)app.Context.Items[this];

                requestMutex.Wait();

                GC.KeepAlive(app);
            }, null);

            mutex.Wait();
        }

        private void EndEvent(IAsyncResult ar)
        {
        }

        private class AsyncResult : IAsyncResult
        {
            public bool IsCompleted { get; set; }
            public WaitHandle AsyncWaitHandle => WaitHandle;
            public object AsyncState { get; set; }
            public bool CompletedSynchronously { get; set; }

            internal EventWaitHandle WaitHandle { get; } = new EventWaitHandle(false, EventResetMode.ManualReset);
        }
    }
}
