
using System;
using System.IO;
using System.Runtime.InteropServices;
using NLog;
using NLog.Config;
using NLog.Targets;
using PluginApplication;

namespace LogsInjection.NLog
{
    public class Program
    {
        public static int Main(string[] args)
        {
            // This test creates and unloads an appdomain
            // It seems that in some (unknown) conditions the tracer gets loader into the child appdomain
            // When that happens, there is a risk that the startup log thread gets aborted during appdomain unload,
            // adding error logs which in turn cause a failure in CI.
            // Disabling the startup log at the process level should prevent this.
            Environment.SetEnvironmentVariable("DD_TRACE_STARTUP_LOGS", "0");

            LoggingMethods.DeleteExistingLogs();

            // Initialize NLog
            var appDirectory = Directory.GetParent(typeof(Program).Assembly.Location).FullName;
#if NLOG_4_0
            LogManager.Configuration = new XmlLoggingConfiguration(Path.Combine(appDirectory, "NLog.40.config"));
#else
            LogManager.Configuration = new XmlLoggingConfiguration(Path.Combine(appDirectory, "NLog.Pre40.config"));
#endif
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

            Logger Logger = LogManager.GetCurrentClassLogger();
            return LoggingMethods.RunLoggingProcedure(Logger.Info);
        }
    }
}
