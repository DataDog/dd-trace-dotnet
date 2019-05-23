using System;
using System.Collections.Specialized;
using System.IO;
using System.Reflection;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using System.Web.Http;

namespace Samples.AspNetMvc5
{
    public class MvcApplication : HttpApplication
    {
        private static string ProfilerClsId = "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}";
        private static string Hostname = "samplesmvc5";
        private static string IisAgentHost = "dotnetmockagent";

        protected void Application_Start()
        {
            GlobalConfiguration.Configure(WebApiConfig.Register);
            AreaRegistration.RegisterAllAreas();
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            //SetEnvironmentVariableDefaults();
        }

        public void SetEnvironmentVariableDefaults()
        {
            Environment.SetEnvironmentVariable("COR_ENABLE_PROFILING", "1");
            Environment.SetEnvironmentVariable("COR_PROFILER", ProfilerClsId);
            Environment.SetEnvironmentVariable("COR_PROFILER_PATH", GetProfilerPath());

            Environment.SetEnvironmentVariable("DD_PROFILER_PROCESSES", @"iisexpress.exe;w3wp.exe;");

            string integrations = string.Join(";", GetIntegrationsFilePaths());
            Environment.SetEnvironmentVariable("DD_INTEGRATIONS", integrations);
            Environment.SetEnvironmentVariable("DD_TRACE_AGENT_HOSTNAME", IisAgentHost);
            Environment.SetEnvironmentVariable("DD_TRACE_AGENT_PORT", "80");
        }

        private string[] GetIntegrationsFilePaths()
        {
            string fileName = "integrations.json";

            var relativePath = Path.Combine(
                "profiler-lib",
                fileName);

            var fileLocation = Path.Combine(
                GetWebAppBin(),
                relativePath);

            if (!File.Exists(fileLocation))
            {
                throw new Exception($"Unable to find at {fileLocation}");
            }

            return new[]
            {
                fileLocation
            };
        }

        private string GetProfilerPath()
        {
            string fileName = $"Datadog.Trace.ClrProfiler.Native.dll";

            var relativePath = Path.Combine(
                "profiler-lib",
                fileName);

            var fileLocation = Path.Combine(
                GetWebAppBin(),
                relativePath);

            if (!File.Exists(fileLocation))
            {
                throw new Exception($"Unable to find at {fileLocation}");
            }

            return fileLocation;
        }

        private string GetWebAppBin()
        {
            var applicationBin = System.Web.Hosting.HostingEnvironment.MapPath("~/bin");
            if (applicationBin == null)
            {
                throw new Exception("Unable to get bin for web app.");
            }
            return applicationBin;
        }

    }
}
