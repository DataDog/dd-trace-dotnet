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

            var env = SampleHelpers.GetDatadogEnvironmentVariables();
            foreach(var kvp in env)
            {
                Console.WriteLine($"{kvp.Key}: {kvp.Value}");
            }

            bool isAttached = SampleHelpers.IsProfilerAttached();
            Console.WriteLine(" * Checking if the profiler is attached: {0}", isAttached);

            LoggingMethods.DeleteExistingLogs();

            // Initialize NLog
            var appDirectory = Directory.GetParent(typeof(Program).Assembly.Location).FullName;
#if NLOG_4_6
            LogManager.Configuration = new XmlLoggingConfiguration(Path.Combine(appDirectory, "NLog.46.config"));
            Console.WriteLine("Using NLOG_4_6 configuration");

            global::NLog.LogManager.ThrowExceptions = true;
            global::NLog.Common.InternalLogger.LogToConsole = true;
            global::NLog.Common.InternalLogger.LogLevel = LogLevel.Debug;
#elif NLOG_4_0
            LogManager.Configuration = new XmlLoggingConfiguration(Path.Combine(appDirectory, "NLog.40.config"));
            Console.WriteLine("Using NLOG_4_0 configuration");

            global::NLog.LogManager.ThrowExceptions = true;
            global::NLog.Common.InternalLogger.LogToConsole = true;
            global::NLog.Common.InternalLogger.LogLevel = LogLevel.Debug;
#else
            LogManager.Configuration = new XmlLoggingConfiguration(Path.Combine(appDirectory, "NLog.Pre40.config"));
            Console.WriteLine("Using pre NLOG_4_0 configuration");
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

        public static IEnumerable<KeyValuePair<string,string>> GetDatadogEnvironmentVariables()
        {
            var prefixes = new[] { "COR_", "CORECLR_", "DD_", "DATADOG_" };

            var envVars = from envVar in Environment.GetEnvironmentVariables().Cast<DictionaryEntry>()
                          from prefix in prefixes
                          let key = (envVar.Key as string)?.ToUpperInvariant()
                          let value = envVar.Value as string
                          where key.StartsWith(prefix)
                          orderby key
                          select new KeyValuePair<string, string>(key, value);

            return envVars.ToList();
        }
    }
}
