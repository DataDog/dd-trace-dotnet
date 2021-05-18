
using System;
using System.IO;
using System.Threading;
using Datadog.Trace;
using Datadog.Trace.Configuration;
using log4net;
using log4net.Config;

namespace ApplicationWithLog4Net
{
    public class Program : MarshalByRefObject
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(Program));

        public void Invoke()
        {
            var logRepository = LogManager.GetRepository(typeof(Program).Assembly);
            XmlConfigurator.Configure(logRepository, new FileInfo("log4net.config"));

            // Write some log message
            log.Info("Message");

            // Set up Tracer
            var settings = new TracerSettings()
            {
                LogsInjectionEnabled = true
            };
            Tracer.Instance = new Tracer(settings);

            Thread.Sleep(1000);
        }
    }
}
