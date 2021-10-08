
using System;
using System.IO;
using log4net;
using log4net.Config;
using PluginApplication;

namespace LogsInjection.Log4Net
{
    public class Program
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(Program));

        public static int Main(string[] args)
        {
            // This test creates and unloads an appdomain
            // It seems that in some (unknown) conditions the tracer gets loader into the child appdomain
            // When that happens, there is a risk that the startup log thread gets aborted during appdomain unload,
            // adding error logs which in turn cause a failure in CI.
            // Disabling the startup log at the process level should prevent this.
            Environment.SetEnvironmentVariable("DD_TRACE_STARTUP_LOGS", "0");

            LoggingMethods.DeleteExistingLogs();

            // Initialize log4net
            var logRepository = LogManager.GetRepository(typeof(Program).Assembly);
            var appDirectory = Directory.GetParent(typeof(Program).Assembly.Location).FullName;
#if NETFRAMEWORK && LOG4NET_2_0_5
            XmlConfigurator.Configure(logRepository, new FileInfo(Path.Combine(appDirectory, "log4net.205.config")));
#else
            // Regardless of package version, for .NET Core just assert against raw log lines
            XmlConfigurator.Configure(logRepository, new FileInfo(Path.Combine(appDirectory, "log4net.Pre205.config")));
#endif

            return LoggingMethods.RunLoggingProcedure(log.Info);
        }
    }
}
