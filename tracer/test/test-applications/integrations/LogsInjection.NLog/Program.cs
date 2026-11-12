using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using NLog;
using NLog.Config;
using NLog.Targets;
using PluginApplication;

#pragma warning disable CS0618 // MDC and MDLC are obsolete in newer NLog versions
#if NLOG_4_0
using Mdc = NLog.MappedDiagnosticsContext;
#else
using Mdc = NLog.MDC;
#endif

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
            // It seems that in some (unknown) conditions the tracer gets loaded into the child appdomain
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
            const string configFile = "NLog.50.config";
#elif NLOG_4_6
            const string configFile = "NLog.46.config";
#elif NLOG_4_0
            const string configFile = "NLog.40.config";
#else
            const string configFile = "NLog.Pre40.config";
#endif
#if NLOG_4_0
            global::NLog.LogManager.ThrowExceptions = true;
            global::NLog.Common.InternalLogger.LogToConsole = true;
            global::NLog.Common.InternalLogger.LogLevel = LogLevel.Debug;
#endif
            if (configType == ConfigurationType.Both)
            {
                LogManager.Configuration = new XmlLoggingConfiguration(Path.Combine(appDirectory, "Configurations", configFile));
            }

            Console.WriteLine($"Using {configFile}: Configuration type is: {configType}");
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

#if NETFRAMEWORK && NLOG_4_6
            // Keep logical context alive across the helper's AppDomain call. The per-message contexts
            // below are removed before that call, which would otherwise miss serialization regressions.
            const string crossDomainContextKey = "CrossDomainContextKey";
            const string crossDomainContextValue = "CrossDomainContextValue";
            var includeCrossDomainCall = Environment.GetEnvironmentVariable("INCLUDE_CROSS_DOMAIN_CALL") != "false";
#if NLOG_5_0
            using var crossDomainContext = includeCrossDomainCall ? global::NLog.ScopeContext.PushProperty(crossDomainContextKey, crossDomainContextValue) : null;
#else
            using var crossDomainContext = includeCrossDomainCall ? global::NLog.MappedDiagnosticsLogicalContext.SetScoped(crossDomainContextKey, crossDomainContextValue) : null;
#endif
#endif
            var exitCode = LoggingMethods.RunLoggingProcedure(message => LogWithEachContext(logger, message));
#if NETFRAMEWORK && NLOG_4_6
            if (includeCrossDomainCall && global::NLog.MappedDiagnosticsLogicalContext.Get(crossDomainContextKey) != crossDomainContextValue)
            {
                throw new InvalidOperationException("The logging context was lost across the AppDomain call.");
            }
#endif
            return exitCode;
        }

        // A single run covers every context this NLog version supports; NLogTests checks each one using the "(context: X)" suffix
        private static void LogWithEachContext(Logger logger, string message)
        {
            logger.Info($"{message} (context: None)");

            Mdc.Set(CustomContextKey, CustomContextValue);
            try
            {
                logger.Info($"{message} (context: Mdc)");
            }
            finally
            {
                Mdc.Remove(CustomContextKey);
            }

#if NLOG_4_1
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
            if (NativeMethodsType is null)
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
    }
}
