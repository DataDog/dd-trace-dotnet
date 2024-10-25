// <copyright file="Startup.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.IO;
using System.Reflection;
using System.Threading;

namespace Datadog.Trace.ClrProfiler.Managed.Loader
{
    /// <summary>
    /// A class that attempts to load the Datadog.Trace .NET assembly.
    /// </summary>
    public partial class Startup
    {
        private const string AssemblyName = "Datadog.Trace, Version=3.4.1.0, Culture=neutral, PublicKeyToken=def86d061d0d2eeb";
        private const string AzureAppServicesKey = "DD_AZURE_APP_SERVICES";

        private static int _startupCtorInitialized;

        /// <summary>
        /// Initializes static members of the <see cref="Startup"/> class.
        /// This method also attempts to load the Datadog.Trace .NET assembly.
        /// </summary>
        static Startup()
        {
            if (Interlocked.Exchange(ref _startupCtorInitialized, 1) != 0)
            {
                // Startup() was already called before in the same AppDomain, this can happen because the profiler rewrites
                // methods before the jitting to inject the loader. This is done until the profiler detects that the loader
                // has been initialized.
                // The piece of code injected already includes an Interlocked condition but, because the static variable is emitted
                // in a custom type inside the running assembly, others assemblies will also have a different type with a different static
                // variable, so, we still can hit an scenario where multiple loaders initialize.
                // With this we prevent this scenario.
                return;
            }

            try
            {
                ManagedProfilerDirectory = ResolveManagedProfilerDirectory();
                if (ManagedProfilerDirectory is null)
                {
                    StartupLogger.Log("Managed profiler directory doesn't exist. Automatic instrumentation will be disabled");
                    return;
                }

                StartupLogger.Debug("Resolving managed profiler directory to: {0}", ManagedProfilerDirectory);

                try
                {
                    AppDomain.CurrentDomain.AssemblyResolve += AssemblyResolve_ManagedProfilerDependencies;
                }
                catch (Exception ex)
                {
                    StartupLogger.Log(ex, "Unable to register a callback to the CurrentDomain.AssemblyResolve event.");
                }

#if NETCOREAPP
                try
                {
                    System.Runtime.Loader.AssemblyLoadContext.Default.Resolving += (_, assemblyName) => ResolveAssembly(assemblyName.Name);
                }
                catch (Exception ex)
                {
                    StartupLogger.Log(ex, "Unable to register a callback to the AssemblyLoadContext.Default.Resolving event.");
                }
#endif

                var runInAas = ReadBooleanEnvironmentVariable(AzureAppServicesKey, false);
                if (runInAas)
                {
                    // With V3, pretty much all scenarios require the trace-agent and dogstatsd, so we enable them by default
                    StartupLogger.Log("Invoking managed method to start external processes.");
                    TryInvokeManagedMethod("Datadog.Trace.AgentProcessManager", "Initialize", "Datadog.Trace.AgentProcessManagerLoader");
                }

                // We need to invoke the managed tracer regardless of whether tracing is enabled
                // because other products rely on it
                StartupLogger.Log("Invoking managed tracer.");
                TryInvokeManagedMethod("Datadog.Trace.ClrProfiler.Instrumentation", "Initialize", "Datadog.Trace.ClrProfiler.InstrumentationLoader");
            }
            catch (Exception ex)
            {
                try
                {
                    StartupLogger.Log(ex, "Error in Datadog.Trace.ClrProfiler.Managed.Loader.Startup.Startup(). Functionality may be impacted.");
                    return;
                }
                catch
                {
                    // Nothing to do here.
                }

                // If the logger fails, throw the original exception. The profiler emits code to log it.
                throw;
            }
        }

        internal static string? ManagedProfilerDirectory { get; }

        private static void TryInvokeManagedMethod(string typeName, string methodName, string? loaderHelperTypeName = null)
        {
            try
            {
                StartupLogger.Debug("Invoking: '{0}.{1}', {2}", typeName, methodName, loaderHelperTypeName);
                var assembly = LoadAssembly(AssemblyName);
                if (assembly == null)
                {
                    StartupLogger.Log("Assembly '{0}' cannot be loaded. The managed method ({1}.{2}) cannot be invoked", AssemblyName, typeName, methodName);
                    return;
                }

                if (loaderHelperTypeName is not null)
                {
                    // The loader helper type name is a class that calls the initialization in the .ctor
                    // this way we avoid the reflection invoke call.
                    if (assembly.GetType(loaderHelperTypeName, throwOnError: false) is { } loaderHelperType)
                    {
                        StartupLogger.Debug("Creating '{0}' instance.", loaderHelperTypeName);
                        Activator.CreateInstance(loaderHelperType);
                        return;
                    }

                    StartupLogger.Log("Loader Helper '{0}' cannot be found. Invoking {1}.{2}()", loaderHelperTypeName, typeName, methodName);
                }

                var type = assembly.GetType(typeName, throwOnError: false);
                var method = type?.GetRuntimeMethod(methodName, parameters: Type.EmptyTypes);
                StartupLogger.Debug("Calling method '{0}.{1}'.", typeName, methodName);
                method?.Invoke(obj: null, parameters: null);
            }
            catch (Exception ex)
            {
                StartupLogger.Log(ex, "Error when invoking managed method: {0}.{1}", typeName, methodName);
            }
        }

        private static Assembly? LoadAssembly(string assemblyString)
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
                    StartupLogger.Log("Assembly '{0}' was resolved manually.", assemblyString);
                }

                return assembly;
            }
        }

        private static string? ReadEnvironmentVariable(string key)
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
