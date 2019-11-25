using System.IO;
using Datadog.Trace;
using log4net;
using log4net.Config;

namespace Log4NetExample
{
    class Program
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(Program));

        static void Main(string[] args)
        {
            var logRepository = LogManager.GetRepository(typeof(Program).Assembly);
            XmlConfigurator.Configure(logRepository, new FileInfo("log4net.config"));

            LogicalThreadContext.Properties["order-number"] = 1024;
            log.Info("Message before a trace.");
            using (var scope = Tracer.Instance.StartActive("Log4NetExample - Main()"))
            {
                log.Info("Message during a trace.");
            }

            log.Info("Message after a trace.");
        }
    }
}
