using System;
using System.Web;
using Datadog.Trace;

namespace Samples.AspNetAsyncHandler
{
    public class HttpHandler : IHttpHandler
    {
        public bool IsReusable => true;

        public void ProcessRequest(HttpContext context)
        {
            if (context.Request.Path.Contains("shutdown"))
            {
                Shutdown();
                return;
            }

            Tracer.Instance.StartActive("HttpHandler").Dispose();

            context.Response.StatusCode = 200;
            context.Response.Write("Success");
        }

        private void Shutdown()
        {
            SampleHelpers.RunShutDownTasks(this);
        }
    }
}
