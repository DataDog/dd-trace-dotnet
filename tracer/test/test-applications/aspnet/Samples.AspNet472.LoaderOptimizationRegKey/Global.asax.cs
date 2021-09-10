using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Optimization;
using System.Web.Routing;
using System.Web.Security;
using System.Web.SessionState;

namespace Samples.AspNet472.LoaderOptimizationRegKey
{
    public class Global : HttpApplication
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(Global));

        void Application_Start(object sender, EventArgs e)
        {
            // Code that runs on application startup
            RouteConfig.RegisterRoutes(RouteTable.Routes);

            InitializeTrace("Executing from Application_Start");
        }

        public static void InitializeTracePreStart()
        {
            InitializeTrace("Samples.AspNet472.LoaderOptimizationRegKey.InitializeTracePreStart");
        }

        public static void InitializeTrace(string callingMethod)
        {
            log.Info(nameof(InitializeTrace) + " : Executing from " + callingMethod);

            var request = WebRequest.Create("https://icanhazdadjoke.com/");
            ((HttpWebRequest)request).Accept = "application/json;q=1";

            using (var response = (HttpWebResponse)request.GetResponse())
            using (var stream = response.GetResponseStream())
            using (var reader = new StreamReader(stream))
            {
                string responseText = String.Empty;

                try
                {
                    responseText = reader.ReadToEnd();
                }
                catch (Exception)
                {
                    responseText = "ENCOUNTERED AN ERROR WHEN READING RESPONSE.";
                }
                finally
                {
                    log.Info($"Response Text: {responseText}");
                }
            }
        }
    }
}
