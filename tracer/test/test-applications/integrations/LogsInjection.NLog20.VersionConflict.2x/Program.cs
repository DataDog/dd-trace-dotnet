using System.IO;
using LogsInjectionHelper.VersionConflict;
using NLog;
using NLog.Config;

namespace LogsInjection.NLog20.VersionConflict_2x
{
    public class Program
    {
        public static int Main(string[] args)
        {
            var appDirectory = Directory.GetParent(typeof(Program).Assembly.Location).FullName;
            var textFilePath = Path.Combine(appDirectory, "log-textFile.log");

            File.Delete(textFilePath);

            // Initialize NLog
            LogManager.Configuration = new XmlLoggingConfiguration(Path.Combine(appDirectory, "NLog.Pre40.config"));
            Logger logger = LogManager.GetCurrentClassLogger();

            return LoggingMethods.RunLoggingProcedure(logger.Info);
        }
    }
}
