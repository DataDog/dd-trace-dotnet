using System.IO;
using System.Runtime.InteropServices;
using LogsInjectionHelper.VersionConflict;
using NLog;
using NLog.Config;
using NLog.Targets;

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
#if NETCOREAPP
            // Hacks for the fact the NLog on Linux just can't do anything right
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var target = (FileTarget)LogManager.Configuration.FindTargetByName("textFile");
                if (target is not null)
                {
                    target.FileName = Path.Combine(appDirectory, "log-textFile.log");
                }

                target = (FileTarget)LogManager.Configuration.FindTargetByName("jsonFile");
                if (target is not null)
                {
                    target.FileName = Path.Combine(appDirectory, "log-jsonFile.log");
                }
                LogManager.ReconfigExistingLoggers();
            }
#endif

            Logger logger = LogManager.GetCurrentClassLogger();
            return LoggingMethods.RunLoggingProcedure(logger.Info);
        }
    }
}
