using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Linq;
using System.Reflection;
using NLog;
using NLog.Config;
using NLog.Targets;
using PluginApplication;

#pragma warning disable CS0618 // MappedDiagnosticsLogicalContext is obsolete in NLog 5+
namespace LogsInjection.NLog
{
    public class Program
    {
        private const string CustomContextKey = "CustomContextKey";
        private const string CustomContextValue = "CustomContextValue";

        private enum ConfigurationType
        {
            /// <summary>
            /// No configuration provided at all.
            /// </summary>
            None,

            /// <summary>
            /// Configuration file contains targets that are and aren't pre-configured with logs injection related elements.
            /// (e.g., "includeMdc = true" is present in one JSON target, and omitted from the other)
            /// </summary>
            Both
        }

        public static int Main(string[] args)
        {
            // This test creates and unloads an appdomain
            // It seems that in some (unknown) conditions the tracer gets loader into the child appdomain
            // When that happens, there is a risk that the startup log thread gets aborted during appdomain unload,
            // adding error logs which in turn cause a failure in CI.
            // Disabling the startup log at the process level should prevent this.
            Environment.SetEnvironmentVariable("DD_TRACE_STARTUP_LOGS", "0");

            bool isAttached = SampleHelpers.IsProfilerAttached();
            Console.WriteLine(" * Checking if the profiler is attached: {0}", isAttached);

            LoggingMethods.DeleteExistingLogs();

            var configType = ConfigurationType.Both;
            if (args.Length >= 1 && !Enum.TryParse(args[0], true, out configType))
            {
                throw new ArgumentException($"Failed to parse configuration type for NLog sample '{args[0]}'", args[0]);
            }

            // Initialize NLog
            var appDirectory = Directory.GetParent(typeof(Program).Assembly.Location).FullName;
#if NLOG_5_0
            if (configType == ConfigurationType.Both)
            {
                LogManager.Configuration = new XmlLoggingConfiguration(Path.Combine(appDirectory, "Configurations", "NLog.50.config"));
            }

            Console.WriteLine($"Using NLOG_5_0: Configuration type is: {configType}");

            global::NLog.LogManager.ThrowExceptions = true;
            global::NLog.Common.InternalLogger.LogToConsole = true;
            global::NLog.Common.InternalLogger.LogLevel = LogLevel.Debug;
#elif NLOG_4_6
            if (configType == ConfigurationType.Both)
            {
                LogManager.Configuration = new XmlLoggingConfiguration(Path.Combine(appDirectory, "Configurations", "NLog.46.config"));
            }

            Console.WriteLine($"Using NLOG_4_6: Configuration type is: {configType}");

            global::NLog.LogManager.ThrowExceptions = true;
            global::NLog.Common.InternalLogger.LogToConsole = true;
            global::NLog.Common.InternalLogger.LogLevel = LogLevel.Debug;
#elif NLOG_4_0
            global::NLog.LogManager.ThrowExceptions = true;
            global::NLog.Common.InternalLogger.LogToConsole = true;
            global::NLog.Common.InternalLogger.LogLevel = LogLevel.Debug;

            if (configType == ConfigurationType.Both)
            {
                LogManager.Configuration = new XmlLoggingConfiguration(Path.Combine(appDirectory, "Configurations", "NLog.40.config"));
            }

            Console.WriteLine($"Using NLOG_4_0: Configuration type is: {configType}");
#else
            if (configType == ConfigurationType.Both)
            {
                LogManager.Configuration = new XmlLoggingConfiguration(Path.Combine(appDirectory, "Configurations", "NLog.Pre40.config"));
            }

            Console.WriteLine($"Using pre NLOG_4_0: Configuration type is: {configType}");
#endif
#if NETCOREAPP
            // Hacks for the fact the NLog on Linux just can't do anything right
            // When on ConfigurationType.None LogManager.Configuration is going to be null - so need to skip
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) &&
                configType != ConfigurationType.None)
            {
                var target = (FileTarget)LogManager.Configuration.FindTargetByName("textFile-withInject");
                if (target is not null)
                {
                    target.FileName = Path.Combine(appDirectory, "log-textFile-withInject.log");
                }

                target = (FileTarget)LogManager.Configuration.FindTargetByName("jsonFile-withInject");
                if (target is not null)
                {
                    target.FileName = Path.Combine(appDirectory, "log-jsonFile-withInject.log");
                }

                // the one without Log Injection stuff
                target = (FileTarget)LogManager.Configuration.FindTargetByName("jsonFile-noInject");
                if (target is not null)
                {
                    target.FileName = Path.Combine(appDirectory, "log-jsonFile-noInject.log");
                }

                LogManager.ReconfigExistingLoggers();
            }
#endif

            Logger logger = LogManager.GetCurrentClassLogger();
            return LoggingMethods.RunLoggingProcedure(message => LogWithEachContext(logger, message));
        }

        // A single run covers every context this NLog version supports; NLogTests checks each one using the "(context: X)" suffix
        private static void LogWithEachContext(Logger logger, string message)
        {
            logger.Info($"{message} (context: None)");

#if NLOG_4_6
            global::NLog.MappedDiagnosticsLogicalContext.Set(CustomContextKey, CustomContextValue);
            try
            {
                logger.Info($"{message} (context: Mdlc)");
            }
            finally
            {
                global::NLog.MappedDiagnosticsLogicalContext.Remove(CustomContextKey);
            }
#endif

#if NLOG_5_0
            using (global::NLog.ScopeContext.PushProperty(CustomContextKey, CustomContextValue))
            {
                logger.Info($"{message} (context: ScopeContext)");
            }
#endif
        }
    }

    public class SampleHelpers
    {
        private static readonly Type NativeMethodsType = Type.GetType("Datadog.Trace.ClrProfiler.NativeMethods, Datadog.Trace");

        public static bool IsProfilerAttached()
        {
            if(NativeMethodsType is null)
            {
                return false;
            }

            try
            {
                MethodInfo profilerAttachedMethodInfo = NativeMethodsType.GetMethod("IsProfilerAttached");
                return (bool)profilerAttachedMethodInfo.Invoke(null, null);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            return false;
        }

        public static string GetTracerAssemblyLocation()
        {
            return NativeMethodsType?.Assembly.Location ?? "(none)";
        }

        public static void RunShutDownTasks(object caller)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (var assembly in assemblies)
            {
                foreach (var type in assembly.DefinedTypes)
                {
                    if (type.Namespace == "Coverlet.Core.Instrumentation.Tracker")
                    {
                        var unloadModuleMethod = type.GetMethod("UnloadModule", BindingFlags.Public | BindingFlags.Static);
                        unloadModuleMethod.Invoke(null, new object[] { caller, EventArgs.Empty });
                    }
                }
            }
        }
    }
}
