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

#pragma warning disable CS0618 // MappedDiagnosticContext is obsolete
namespace LogsInjection.NLog
{
    public class Program
    {
        private enum ContextProperty
        {
            None,
            Mdc,
            Mdlc,
            ScopeContext
        }

        private enum ConfigurationType
        {
            /// <summary>
            /// No configuration provided at all.
            /// </summary>
            None,

            /// <summary>
            /// All targets in configuration will _not_ contain targets pre-configured with logs injection related elements.
            /// (e.g., "includeMdc = true" would be omitted from the JSON target)
            /// </summary>
            NoLogsInjection,

            /// <summary>
            /// All targets in configuration _will_ contain targets pre-configured with logs injection related elements.
            /// (e.g., "includeMdc = true" would be present in the JSON target)
            /// </summary>
            LogsInjection,

            /// <summary>
            /// Configuration file contains targets that are and aren't pre-configured with logs injection related elements.
            /// </summary>
            Both
        }

        private enum DirectLogSubmission
        {
            /// <summary>
            /// DirectLogSubmission is enabled.
            /// </summary>
            Enable,

            /// <summary>
            /// DirectLogSubmission is disabled.
            /// </summary>
            Disable
        }

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

            var contextProperty = ContextProperty.None;
            if (args.Length > 0 && !ContextProperty.TryParse(args[0], true, out contextProperty))
            {
                throw new ArgumentException($"Invalid context property '{args[0]}'", args[0]);
            }

            var configType = ConfigurationType.Both;
            if (args.Length >= 2 && !Enum.TryParse(args[1], true, out configType))
            {
                throw new ArgumentException($"Failed to parse configuration type for NLog sample '{args[1]}'", args[1]);
            }

            Console.WriteLine("Context property injection: {0}", contextProperty);

            // Initialize NLog
            var appDirectory = Directory.GetParent(typeof(Program).Assembly.Location).FullName;
#if NLOG_5_0
            switch (configType)
            {
                case ConfigurationType.None:
                    break;
                case ConfigurationType.NoLogsInjection:
                    LogManager.Configuration = new XmlLoggingConfiguration(Path.Combine(appDirectory, "Configurations", "NLog.50.NoLogsInjection.config"));
                    break;
                case ConfigurationType.LogsInjection:
                    LogManager.Configuration = new XmlLoggingConfiguration(Path.Combine(appDirectory, "Configurations", "NLog.50.WithLogsInjection.config"));
                    break;
                case ConfigurationType.Both:
                    LogManager.Configuration = new XmlLoggingConfiguration(Path.Combine(appDirectory, "Configurations", "NLog.50.config"));
                    break;
            }

            Console.WriteLine($"Using NLOG_5_0: Configuration type is: {configType}");

            global::NLog.LogManager.ThrowExceptions = true;
            global::NLog.Common.InternalLogger.LogToConsole = true;
            global::NLog.Common.InternalLogger.LogLevel = LogLevel.Debug;
#elif NLOG_4_6
            switch (configType)
            {
                case ConfigurationType.None:
                    break;
                case ConfigurationType.NoLogsInjection:
                    LogManager.Configuration = new XmlLoggingConfiguration(Path.Combine(appDirectory, "Configurations", "NLog.46.NoLogsInjection.config"));
                    break;
                case ConfigurationType.LogsInjection:
                    LogManager.Configuration = new XmlLoggingConfiguration(Path.Combine(appDirectory, "Configurations", "NLog.46.WithLogsInjection.config"));
                    break;
                case ConfigurationType.Both:
                    LogManager.Configuration = new XmlLoggingConfiguration(Path.Combine(appDirectory, "Configurations", "NLog.46.config"));
                    break;
            }

            Console.WriteLine($"Using NLOG_4_6: Configuration type is: {configType}");

            global::NLog.LogManager.ThrowExceptions = true;
            global::NLog.Common.InternalLogger.LogToConsole = true;
            global::NLog.Common.InternalLogger.LogLevel = LogLevel.Debug;
#elif NLOG_4_0
            global::NLog.LogManager.ThrowExceptions = true;
            global::NLog.Common.InternalLogger.LogToConsole = true;
            global::NLog.Common.InternalLogger.LogLevel = LogLevel.Debug;

            switch (configType)
            {
                case ConfigurationType.None:
                    break;
                case ConfigurationType.NoLogsInjection:
                    LogManager.Configuration = new XmlLoggingConfiguration(Path.Combine(appDirectory, "Configurations", "NLog.40.NoLogsInjection.config"));
                    break;
                case ConfigurationType.LogsInjection:
                    LogManager.Configuration = new XmlLoggingConfiguration(Path.Combine(appDirectory, "Configurations", "NLog.40.WithLogsInjection.config"));
                    break;
                case ConfigurationType.Both:
                    LogManager.Configuration = new XmlLoggingConfiguration(Path.Combine(appDirectory, "Configurations", "NLog.40.config"));
                    break;
            }

            Console.WriteLine($"Using NLOG_4_0: Configuration type is: {configType}");
#else
            switch (configType)
            {
                case ConfigurationType.None:
                    break;
                case ConfigurationType.NoLogsInjection:
                    throw new InvalidOperationException("The pre NLOG_4_0 configurations don't have JSON support so no auto-configured logs injection either.");
                case ConfigurationType.LogsInjection:
                    LogManager.Configuration = new XmlLoggingConfiguration(Path.Combine(appDirectory, "Configurations", "NLog.Pre40.WithLogsInjection.config"));
                    break;
                case ConfigurationType.Both:
                    LogManager.Configuration = new XmlLoggingConfiguration(Path.Combine(appDirectory, "Configurations", "NLog.Pre40.config"));
                    break;
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

                // ones without Log Injection stuff
                target = (FileTarget)LogManager.Configuration.FindTargetByName("textFile-noInject");
                if (target is not null)
                {
                    target.FileName = Path.Combine(appDirectory, "log-textFile-noInject.log");
                }

                target = (FileTarget)LogManager.Configuration.FindTargetByName("jsonFile-noInject");
                if (target is not null)
                {
                    target.FileName = Path.Combine(appDirectory, "log-jsonFile-noInject.log");
                }

                LogManager.ReconfigExistingLoggers();
            }
#endif

            return LoggingMethods.RunLoggingProcedure(message => AddToContextAndLog(message, contextProperty));
        }

        private static void AddToContextAndLog(string message, ContextProperty contextProperty)
        {
            string propKey = "CustomContextKey", propValue = "CustomContextValue";
            // don't complain if they're not actually used.
            _ = propKey;
            _ = propValue;

            switch (contextProperty)
            {
                case ContextProperty.ScopeContext:
#if NLOG_5_0
                    global::NLog.ScopeContext.PushProperty(propKey, propValue);
                    break;
#else
                    throw new ArgumentException($"Invalid context property '{contextProperty}' for this NLog version", contextProperty.ToString());
#endif
                case ContextProperty.Mdlc:
#if NLOG_5_0 || NLOG_4_6
                    global::NLog.MappedDiagnosticsLogicalContext.Set(propKey, propValue);
                    break;
#else
                    throw new ArgumentException("Invalid context property '{0}' for this NLog version", contextProperty.ToString());
#endif
                case ContextProperty.Mdc:
                    global::NLog.MappedDiagnosticsContext.Set(propKey, propValue);
                    break;
            }

            Logger logger = LogManager.GetCurrentClassLogger();
            logger.Info(message);

            // No need to remove properties from context, as program is going to exit immediately
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
