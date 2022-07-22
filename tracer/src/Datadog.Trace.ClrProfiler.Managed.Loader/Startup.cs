// <copyright file="Startup.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Reflection;

namespace Datadog.Trace.ClrProfiler.Managed.Loader
{
    /// <summary>
    /// A class that attempts to load the Datadog.Trace .NET assembly.
    /// </summary>
    public partial class Startup
    {
        private const string AssemblyName = "Datadog.Trace, Version=2.13.0.0, Culture=neutral, PublicKeyToken=def86d061d0d2eeb";
        private const string AzureAppServicesKey = "DD_AZURE_APP_SERVICES";
        private const string AasCustomTracingKey = "DD_AAS_ENABLE_CUSTOM_TRACING";
        private const string AasCustomMetricsKey = "DD_AAS_ENABLE_CUSTOM_METRICS";
        private const string TraceEnabledKey = "DD_TRACE_ENABLED";

        /// <summary>
        /// Initializes static members of the <see cref="Startup"/> class.
        /// This method also attempts to load the Datadog.Trace .NET assembly.
        /// </summary>
        static Startup()
        {
            ManagedProfilerDirectory = ResolveManagedProfilerDirectory();
            StartupLogger.Debug("Resolving managed profiler directory to: {0}", ManagedProfilerDirectory);

            try
            {
                AppDomain.CurrentDomain.AssemblyResolve += AssemblyResolve_ManagedProfilerDependencies;
            }
            catch (Exception ex)
            {
                StartupLogger.Log(ex, "Unable to register a callback to the CurrentDomain.AssemblyResolve event.");
            }

            var runInAas = ReadBooleanEnvironmentVariable(AzureAppServicesKey, false);
            if (!runInAas)
            {
                TryInvokeManagedMethod("Datadog.Trace.ClrProfiler.Instrumentation", "Initialize");
                return;
            }

            // In AAS, the loader can be used to load the tracer, the traceagent only (if only custom tracing is enabled),
            // dogstatsd or all of them.
            var customTracingEnabled = ReadBooleanEnvironmentVariable(AasCustomTracingKey, false);
            var needsDogStatsD = ReadBooleanEnvironmentVariable(AasCustomMetricsKey, false);
            var automaticTraceEnabled = ReadBooleanEnvironmentVariable(TraceEnabledKey, true);

            if (automaticTraceEnabled || customTracingEnabled || needsDogStatsD)
            {
                StartupLogger.Log("Invoking managed method to start external processes.");
                TryInvokeManagedMethod("Datadog.Trace.AgentProcessManager", "Initialize");
            }

            if (automaticTraceEnabled)
            {
                StartupLogger.Log("Invoking managed tracer.");
                TryInvokeManagedMethod("Datadog.Trace.ClrProfiler.Instrumentation", "Initialize");
            }
        }

        internal static string ManagedProfilerDirectory { get; }

        private static void TryInvokeManagedMethod(string typeName, string methodName)
        {
            try
            {
                var assembly = LoadAssembly(AssemblyName);

                if (assembly == null)
                {
                    StartupLogger.Log("Assembly '{0}' cannot be loaded. The managed method ({1}.{2}) cannot be invoked", AssemblyName, typeName, methodName);
                    return;
                }

                var type = assembly.GetType(typeName, throwOnError: false);
                var method = type?.GetRuntimeMethod(methodName, parameters: Type.EmptyTypes);
                method?.Invoke(obj: null, parameters: null);
            }
            catch (Exception ex)
            {
                StartupLogger.Log(ex, "Error when invoking managed method: {0}.{1}", typeName, methodName);
            }
        }

        private static Assembly LoadAssembly(string assemblyString)
        {
            try
            {
                return Assembly.Load(assemblyString);
            }
            catch (FileNotFoundException ex)
            {
                // In some IIS scenarios the `AssemblyResolve` event doesn't get triggered and we received this exception.
                // We will try to resolve it manually as a last chance.
                StartupLogger.Log(ex, "Error on assembly load: {0}, Trying to solve it manually...", assemblyString);

                var assembly = ResolveAssembly(assemblyString);
                if (assembly is not null)
                {
                    StartupLogger.Log("Assembly resolved manually.");
                }

                return assembly;
            }
        }

        private static string ReadEnvironmentVariable(string key)
        {
            try
            {
                return Environment.GetEnvironmentVariable(key);
            }
            catch (Exception ex)
            {
                StartupLogger.Log(ex, "Error while loading environment variable " + key);
            }

            return null;
        }

        private static bool ReadBooleanEnvironmentVariable(string key, bool defaultValue)
        {
            var value = ReadEnvironmentVariable(key);
            return value switch
            {
                "1" or "true" or "True" or "TRUE" or "t" or "T" => true,
                "0" or "false" or "False" or "FALSE" or "f" or "F" => false,
                _ => defaultValue
            };
        }
    }
}
