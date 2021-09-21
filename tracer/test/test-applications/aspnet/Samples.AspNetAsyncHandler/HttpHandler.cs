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
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (var assembly in assemblies)
            {
                foreach (var type in assembly.DefinedTypes)
                {
                    if (type.Namespace == "Coverlet.Core.Instrumentation.Tracker")
                    {
                        var unloadModuleMethod = type.GetMethod("UnloadModule", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                        unloadModuleMethod.Invoke(null, new object[] { this, EventArgs.Empty });
                    }
                }
            }
        }
    }
}
