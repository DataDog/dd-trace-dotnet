// <copyright file="Startup.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;

namespace Datadog.Trace.ClrProfiler.Managed.Loader
{
    /// <summary>
    /// A class that attempts to load the Datadog.Trace .NET assembly.
    /// </summary>
    public sealed partial class Startup
    {
        private const string AssemblyName = "Datadog.Trace, Version=3.38.0.0, Culture=neutral, PublicKeyToken=def86d061d0d2eeb";
        private const string AzureAppServicesSiteExtensionKey = "DD_AZURE_APP_SERVICES"; // only set when using the AAS site extension
        private const string TracerHomePathKey = "DD_DOTNET_TRACER_HOME";

        private static readonly HashSet<string> ArchitectureDirectories = new(StringComparer.OrdinalIgnoreCase)
        {
            "win-x64",
            "win-x86",
#if NETCOREAPP
            "linux-x64",
            "linux-arm64",
            "linux-musl-x64",
            "linux-musl-arm64",
            "osx",
            "osx-arm64",
            "osx-x64"
#endif
        };

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
                // The piece of code injected already includes an Interlocked condition. However, because the static variable is emitted
                // in a custom type inside the running assembly, other assemblies will also have a different type with a different static
                // variable, so we still can hit a scenario where multiple loaders initialize. This prevents this scenario.
                return;
            }

            try
            {
#if NETCOREAPP
                // Check if we're in some sort of AOT scenario
                // Equivalent to checking RuntimeFeature.IsDynamicCodeSupported (added in .NET 8)
                // https://github.com/dotnet/runtime/blob/5535e31a712343a63f5d7d796cd874e563e5ac14/src/libraries/System.Private.CoreLib/src/System/Runtime/CompilerServices/RuntimeFeature.NonNativeAot.cs
                var dynamicCodeSupported = AppContext.TryGetSwitch("System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported", out bool isDynamicCodeSupported) ? isDynamicCodeSupported : true;
                if (!dynamicCodeSupported)
                {
                    // we require dynamic code so we should just bail out ASAP.
                    // This doesn't tell us for sure (the switch is only available on .NET 8+) but it's a minimum requirement
                    StartupLogger.Log("Dynamic code is not supported (System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported context switch is false). Datadog SDK will be disabled.");
                    return;
                }
#endif

                var envVars = new EnvironmentVariableProvider(logErrors: true);
                var tracerHomeDirectory = GetTracerHomePath(envVars);

                if (tracerHomeDirectory is null)
                {
                    // Provide a specific error message based on what was configured
                    var explicitTracerHome = envVars.GetEnvironmentVariable(TracerHomePathKey);

                    if (string.IsNullOrWhiteSpace(explicitTracerHome))
                    {
                        // DD_DOTNET_TRACER_HOME was not set and automatic detection from profiler path failed
                        StartupLogger.Log("{0} is not set and the tracer home directory could not be determined automatically. Datadog SDK will be disabled. To resolve this issue, set environment variable {0}.", TracerHomePathKey);
                    }
                    else
                    {
                        // DD_DOTNET_TRACER_HOME was set but resulted in null (shouldn't happen, but just in case)
                        StartupLogger.Log("{0} is set to '{1}' but could not be used. Datadog SDK will be disabled.", TracerHomePathKey, explicitTracerHome);
                    }

                    return;
                }

                ManagedProfilerDirectory = ComputeTfmDirectory(tracerHomeDirectory);

                if (!Directory.Exists(ManagedProfilerDirectory))
                {
                    StartupLogger.Log("Datadog.Trace.dll directory not found at '{0}'. Datadog SDK will be disabled.", ManagedProfilerDirectory);
                    return;
                }

                StartupLogger.Debug("Resolved Datadog.Trace.dll directory to: {0}", ManagedProfilerDirectory);

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

                const string methodName = "Initialize";
                var usingAasSiteExtension = envVars.GetBooleanEnvironmentVariable(AzureAppServicesSiteExtensionKey) ?? false;

                if (usingAasSiteExtension)
                {
                    // With V3, pretty much all scenarios require the trace-agent and dogstatsd, so we enable them by default
                    const string processManagerTypeName = "Datadog.Trace.AgentProcessManager";
                    StartupLogger.Log("Invoking {0}.{1}() to start external processes.", processManagerTypeName, methodName);
                    TryInvokeManagedMethod(processManagerTypeName, methodName, "Datadog.Trace.AgentProcessManagerLoader");
                }

                // We need to initialize the managed tracer regardless of whether tracing is enabled
                // because other products rely on it
                const string instrumentationTypeName = "Datadog.Trace.ClrProfiler.Instrumentation";
                StartupLogger.Log("Invoking {0}.{1}() to initialize instrumentation.", instrumentationTypeName, methodName);
                TryInvokeManagedMethod(instrumentationTypeName, methodName, "Datadog.Trace.ClrProfiler.InstrumentationLoader");
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

                // If the logger fails, throw the original exception. The native library emits code to log it.
                throw;
            }
        }

        internal static string? ManagedProfilerDirectory { get; }

        internal static string? GetTracerHomePath<TEnvVars>(TEnvVars envVars)
            where TEnvVars : IEnvironmentVariableProvider
        {
            // allow override with DD_DOTNET_TRACER_HOME
            var tracerHomeDirectory = envVars.GetEnvironmentVariable(TracerHomePathKey);

            if (!string.IsNullOrWhiteSpace(tracerHomeDirectory))
            {
                // Safe to use ! here because we just checked !string.IsNullOrWhiteSpace above
                var trimmedPath = tracerHomeDirectory!.Trim();
                StartupLogger.Debug("Using tracer home from {0}=\"{1}\"", TracerHomePathKey, trimmedPath);
                return trimmedPath;
            }

            // try to compute the path from the architecture-specific "COR_PROFILER_PATH_*" or "CORECLR_PROFILER_PATH_*"
            var archEnvVarName = GetProfilerPathEnvVarNameForArch();

            if (ComputeTracerHomePathFromProfilerPath(envVars, archEnvVarName) is { } archTracerHomePath)
            {
                StartupLogger.Debug("Derived tracer home from {0}=\"{1}\"", archEnvVarName, archTracerHomePath);
                return archTracerHomePath;
            }

            // try to compute the path from "COR_PROFILER_PATH" or "CORECLR_PROFILER_PATH" (no architecture)
            var fallbackEnvVarName = GetProfilerPathEnvVarNameFallback();

            if (ComputeTracerHomePathFromProfilerPath(envVars, fallbackEnvVarName) is { } fallbackTracerHomePath)
            {
                StartupLogger.Debug("Derived tracer home from {0}=\"{1}\"", fallbackEnvVarName, fallbackTracerHomePath);
                return fallbackTracerHomePath;
            }

            return null;
        }

        internal static string? ComputeTracerHomePathFromProfilerPath<TEnvVars>(TEnvVars envVars, string envVarName)
            where TEnvVars : IEnvironmentVariableProvider
        {
            var envVarValue = envVars.GetEnvironmentVariable(envVarName)?.Trim();

            if (string.IsNullOrWhiteSpace(envVarValue))
            {
                return null;
            }

            try
            {
                var directory = Directory.GetParent(envVarValue);

                if (directory is null)
                {
                    StartupLogger.Log("Unable to determine tracer home directory from {0}={1}", envVarName, envVarValue);
                    return null;
                }

                // if the directory name is one of the well-known "os-arch" child directories (e.g. "win-x64"), go one level higher
                if (ArchitectureDirectories.Contains(directory.Name))
                {
                    directory = directory.Parent;

                    if (directory is null)
                    {
                        StartupLogger.Log("Unable to determine tracer home directory from {0}={1}", envVarName, envVarValue);
                        return null;
                    }
                }

                return directory.FullName;
            }
            catch (Exception ex)
            {
                StartupLogger.Log(ex, "Error resolving tracer home directory from {0}={1}", envVarName, envVarValue);
                return null;
            }
        }

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
    }
}
