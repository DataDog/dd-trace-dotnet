
using System;
using System.IO;
using Datadog.Trace.ClrProfiler.Integrations;
using log4net;
using log4net.Config;

namespace ApplicationWithLog4Net
{
    public class Program : MarshalByRefObject
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(Program));

        static void Main(string[] args)
        {
            var logRepository = LogManager.GetRepository(typeof(Program).Assembly);
            XmlConfigurator.Configure(logRepository, new FileInfo("log4net.config"));

            log.Info("Message");
        }

        public override string ToString()
        {
            // Set DD_LOGS_INJECTION to enable the automatic trace injection
            Environment.SetEnvironmentVariable("DD_LOGS_INJECTION", "true");

            // Let's call AspNetStartup.Register now
            // This will create a Tracer and, if buggy, store items in the CallContext
            // which will need to be passed by ref or deserialized when transitioning AppDomains
            AspNetStartup.Register();

            return "ApplicationWithLog4Net.Program";
        }
    }
}
