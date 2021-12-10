using System.IO;
using LogsInjectionHelper.VersionConflict;
using NLog;
using NLog.Config;

namespace LogsInjection.NLog.VersionConflict_2x
{
    public class Program
    {
        public static int Main(string[] args)
        {
            var appDirectory = Directory.GetParent(typeof(Program).Assembly.Location).FullName;
            var textFilePath = Path.Combine(appDirectory, "log-textFile.log");
            var jsonFilePath = Path.Combine(appDirectory, "log-jsonFile.log");

            File.Delete(textFilePath);
            File.Delete(jsonFilePath);

            // Initialize NLog
            LogManager.Configuration = new XmlLoggingConfiguration(Path.Combine(appDirectory, "NLog.46.config"));
            Logger logger = LogManager.GetCurrentClassLogger();

            return LoggingMethods.RunLoggingProcedure(logger.Info);
        }
    }
}
