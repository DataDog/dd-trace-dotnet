
using System.IO;
using NLog;
using NLog.Config;
using PluginApplication;

namespace LogsInjection.NLog
{
    public class Program
    {
        public static int Main(string[] args)
        {
            LoggingMethods.DeleteExistingLogs();

            // Initialize NLog
            var appDirectory = Directory.GetParent(typeof(Program).Assembly.Location).FullName;
#if NLOG_4_0
            LogManager.Configuration = new XmlLoggingConfiguration(Path.Combine(appDirectory, "NLog.40.config"));
#else
            LogManager.Configuration = new XmlLoggingConfiguration(Path.Combine(appDirectory, "NLog.Pre40.config"));
#endif

            Logger Logger = LogManager.GetCurrentClassLogger();
            return LoggingMethods.RunLoggingProcedure(Logger.Info);
        }
    }
}
