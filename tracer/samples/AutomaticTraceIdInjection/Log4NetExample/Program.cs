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
            // Uncomment this line if you want to debug your log4net setup
            // log4net.Util.LogLog.InternalDebugging = true;
            var configFilePath = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location), "log4net.config");
            XmlConfigurator.Configure(logRepository, new FileInfo(configFilePath));
            try
            {
                LogicalThreadContext.Properties["order-number"] = 1024;
                log.Info("Message before a trace.");
                using (var scope = Tracer.Instance.StartActive("Log4NetExample - Main()"))
                {
                    log.Info("Message during a trace.");
                }
            }
            finally
            {
                LogicalThreadContext.Properties.Remove("order-number");
            }

            log.Info("Message after a trace.");
        }
    }
}
