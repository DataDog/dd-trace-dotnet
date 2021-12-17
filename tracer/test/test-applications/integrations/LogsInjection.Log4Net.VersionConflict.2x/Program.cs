using System.IO;
using log4net;
using log4net.Config;
using LogsInjectionHelper.VersionConflict;

namespace LogsInjection.Log4Net.VersionConflict_2x
{
    public class Program
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(Program));

        public static int Main(string[] args)
        {
            var appDirectory = Directory.GetParent(typeof(Program).Assembly.Location).FullName;
            var textFilePath = Path.Combine(appDirectory, "log-textFile.log");
            var jsonFilePath = Path.Combine(appDirectory, "log-jsonFile.log");

            File.Delete(textFilePath);
            File.Delete(jsonFilePath);

            // Initialize log4net
            var logRepository = LogManager.GetRepository(typeof(Program).Assembly);
            XmlConfigurator.Configure(logRepository, new FileInfo(Path.Combine(appDirectory, "log4net.205.config")));

            return LoggingMethods.RunLoggingProcedure(log.Info);
        }
    }
}
