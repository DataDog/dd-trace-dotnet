using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using Datadog.Util;

namespace Datadog.AutoInstrumentation.ManagedLoader
{
    /// <summary>
    /// Loads specified assemblies into the current AppDomain.
    ///
    /// This is the only public class in this assembly.
    /// This entire assembly is compiled into the native profiler DLL as a resource.
    /// This happens both, for the profiler part of the Tracer, and for the actual Profiler.
    /// The native code then uses this class to call arbitrary managed code:
    /// It uses IL rewriting to inject a call to <c>AssemblyLoader.Run(..)</c> and passes a list of assemblies.
    /// Then, this class loads all of the specified assemblies and calls a well known entry point in those assemblies.
    /// (See <c>TargetLibraryEntrypointXxx</c> constants in this class.)
    /// If the specified assemblies do not contain such an entry point, they will be loaded, but nothing will be executed.
    ///
    /// This class sets up a basic AppDomain.AssemblyResolve handler to look for the assemblies in a framework-specific subdirectory
    /// of the product home directory in addition to the normal probing paths (e.g. DD_DOTNET_TRACER_HOME for the Tracer).
    /// It also allows for some SxS loading using custom Assembly Load Context.
    ///
    /// If a target assembly needs additional AssemblyResolve event logic to satisfy its dependencies,
    /// or for any other reasons, it must set up its own AssemblyResolve handler as the first thing after its
    /// entry point is called.
    ///
    /// !*! Do not make the AppDomain.AssemblyResolve handler in here more complex !*!
    /// If anything, it should be simplified and any special logic should be moved into the respective assemblies
    /// requested for loading.
    ///
    /// !*! Also, remember that this assembly is shared between the Tracer's profiler component
    /// and the Profiler's profiler component. Do not put specialized code here !*!
    /// </summary>
    public class AssemblyLoader
    {
        /// <summary>
        /// The constants <c>TargetLibraryEntrypointMethod</c>, and <c>...Type</c> specify
        /// which entrypoint to call in the specified assemblies. The respective assembly is expected to have
        /// exactly this entry point. Otherwise, the assembly will be loaded, but nothing will be invoked
        /// explicitly (but be aware of Module cctor caveats).
        /// The method must be static, the return type of the method must be <c>void</c> and it must have no parameters.
        /// Before doing anything else, the target assemblies must set up AppDomain AssemblyResolve events that
        /// make sure that their respective dependencies can be loaded.
        /// </summary>
        public const string TargetLibraryEntrypointMethod = "Run";

        /// <summary> The namespace and the type name of the entrypoint to invoke in each loaded assemby.
        /// More info: <see cref="AssemblyLoader.TargetLibraryEntrypointMethod" />. </summary>
        public const string TargetLibraryEntrypointType = "Datadog.AutoInstrumentation" + "." + "DllMain";

        internal const bool UseConsoleLoggingInsteadOfFile = false;             // Should be False in production.
        internal const bool UseConsoleLogInAdditionToFileLog = false;           // Should be False in production?
        internal const bool UseConsoleLoggingIfFileLoggingFails = true;         // May be True in production. Can that affect customer app behaviour?

        private const string LoggingComponentMoniker = nameof(AssemblyLoader);  // The prefix to this is specified in LogComposer.tt

        private static class ExecuteDelayedConstants
        {
            public const string ThreadName = "DD.Profiler." + nameof(AssemblyLoader) + "." + nameof(AssemblyLoader.ExecuteDelayed);
            public const int SleepDurationMs = 100;

            // Set this env var to FALSE to disable delayed execution:
            public const string IsEnabled_EnvVarName = "DD_INTERNAL_LOADER_DELAY_ENABLED";
            public const bool IsEnabled_DefaultVal = true;

            // Set this env var to a POSITIVE NUMBER to force delayed execution in default IIS app domain:
            public const string IisDelayMs_EnvVarName = "DD_INTERNAL_LOADER_DELAY_IIS_MILLISEC";
            public const int IisDelayMs_DefaultVal = 0;
        }

        private bool _isDefaultAppDomain;
        private string[] _assemblyNamesToLoadIntoDefaultAppDomain;
        private string[] _assemblyNamesToLoadIntoNonDefaultAppDomains;

        /// <summary>
        /// Instantiates an <c>AssemblyLoader</c> instance with the specified assemblies and executes it.
        /// </summary>
        /// <param name="assemblyNamesToLoadIntoDefaultAppDomain">List of assemblies to load and start if the curret App Domain is the default App Domain.</param>
        /// <param name="assemblyNamesToLoadIntoNonDefaultAppDomains">List of assemblies to load and start if the curret App Domain is the NOT default App Domain.</param>
        public static void Run(string[] assemblyNamesToLoadIntoDefaultAppDomain, string[] assemblyNamesToLoadIntoNonDefaultAppDomains)
        {
            try
            {
                var assemblyLoader = new AssemblyLoader(assemblyNamesToLoadIntoDefaultAppDomain, assemblyNamesToLoadIntoNonDefaultAppDomains);
                assemblyLoader.Execute();
            }
            catch
            {
                // An exception escaped from the loader. We are about to return to the caller, which is likely the IL-injected code in the hook.
                // All we can do is log the error and swallow it to avoid crashing things.
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AssemblyLoader"/> class.
        /// </summary>
        /// <param name="assemblyNamesToLoadIntoDefaultAppDomain">List of assemblies to load and start if the curret App Domain is the default App Domain.</param>
        /// <param name="assemblyNamesToLoadIntoNonDefaultAppDomains">List of assemblies to load and start if the curret App Domain is the NOT default App Domain.</param>
        public AssemblyLoader(string[] assemblyNamesToLoadIntoDefaultAppDomain, string[] assemblyNamesToLoadIntoNonDefaultAppDomains)
        {
            _assemblyNamesToLoadIntoDefaultAppDomain = assemblyNamesToLoadIntoDefaultAppDomain;
            _assemblyNamesToLoadIntoNonDefaultAppDomains = assemblyNamesToLoadIntoNonDefaultAppDomains;
        }

        public void Execute()
        {
            try
            {
                if (IsExecutionDelayRequired())
                {
                    Thread executeDelayedThread = new Thread(ExecuteDelayed);
                    executeDelayedThread.Name = ExecuteDelayedConstants.ThreadName;
                    executeDelayedThread.IsBackground = true;
                    executeDelayedThread.Start(this);
                }
                else
                {
                    InitLogAndExecute(this, isDelayed: false, waitForAppDomainReadinessElapsedMs: 0);
                }
            }
            catch (Exception ex)
            {
                LogToConsoleIfEnabled("An error occurred. Assemblies may be not loaded or started.", ex);
            }
        }

        private static void ExecuteDelayed(object assemblyLoaderObj)
        {
            try
            {
                AssemblyLoader assemblyLoader = (AssemblyLoader) assemblyLoaderObj;

                int startDelayMs = Environment.TickCount;

                if (IsAppHostedInIis())
                {
                    int sleepDurationMs = GetIisExecutionDelayMs();
                    Thread.Sleep(sleepDurationMs);
                }
                else
                {
                    while (!IsAppDomainReadyForExecution())
                    {
                        try
                        {
                            Thread.Sleep(ExecuteDelayedConstants.SleepDurationMs);
                        }
                        catch (Exception ex)
                        {
                            // Something unexpected and very bad happened, and we know that the logger in not yet initialized.
                            // We must bail.
                            LogToConsoleIfEnabled("Unexpected error while waiting for AppDomain to become ready for execution."
                                                + " Will not proceed loading assemblies to avoid unwanted side-effects.",
                                                  ex);
                            return;
                        }
                    }
                }

                int totalElapsedDelayMs = Environment.TickCount - startDelayMs;
                InitLogAndExecute(assemblyLoader, isDelayed: true, totalElapsedDelayMs);
            }
            catch
            {
                // Inside of 'InitLogAndExecute(..)' we do everything we can to prevent exceptions from escaping.                
                // Our last choice is to let it escape and potentially crash the process or swallow it. We prefer the latter.
            }
        }

        private static bool IsExecutionDelayRequired()
        {
            //                                                                         We delay IFF:
            return IsExecuteDelayedEnabled()                                        // The user did NOT disable the delay feature;
                        && AppDomain.CurrentDomain.IsDefaultAppDomain()             // AND we are in the default AppDomain;
                        && (!IsAppHostedInIis() || GetIisExecutionDelayMs() >= 1)   // AND we are either NOT in IIS, OR we are in IIS and the user enabled IIS-delay;
                        && !IsAppDomainReadyForExecution();                         // AND the over-time-changing delay-stop contitions are not already met.
        }

        private static bool IsAppDomainReadyForExecution()
        {
            // If the entry assembly IS known, then we are ready.
            return (Assembly.GetEntryAssembly() != null);
        }

        private static void InitLogAndExecute(AssemblyLoader assemblyLoader, bool isDelayed, int waitForAppDomainReadinessElapsedMs)
        {
            try
            {
                LogConfigurator.SetupLogger();
            }
            catch (Exception ex)
            {
                LogToConsoleIfEnabled("An error occurred while initializeing the logging subsystem. This is not an expected state."
                                    + " Will not proceed loading assemblies to avoid unwanted side-effects.",
                                      ex);
                return;
            }

            try
            {
                assemblyLoader.Execute(isDelayed, waitForAppDomainReadinessElapsedMs);
            }
            catch (Exception ex)
            {
                // An exception escaped from the loader.
                // We are about to return to the caller, which is either the IL-injected in the hook or the bottom of the delay-thread.
                // So all we can do is log the error and swallow it to avoid crashing things.
                Log.Error(LoggingComponentMoniker, ex);
            }
        }

        /// <summary>
        /// Loads the assemblies specified for this <c>AssemblyLoader</c> instance and executes their entry point.
        /// </summary>
        private void Execute(bool isDelayed, int waitForAppDomainReadinessElapsedMs)
        {
#if DEBUG
            const string BuildConfiguration = "Debug";
#else
            const string BuildConfiguration = "Release";
#endif
            Log.Info(LoggingComponentMoniker,
                     "Initializing...",
                     "Managed Loader build configuration", BuildConfiguration,
                     nameof(isDelayed), isDelayed,
                     nameof(waitForAppDomainReadinessElapsedMs), waitForAppDomainReadinessElapsedMs,
                     $"{nameof(IsExecuteDelayedEnabled)}()", IsExecuteDelayedEnabled(),
                     $"{nameof(IsAppHostedInIis)}()", IsAppHostedInIis(),
                     $"{nameof(GetIisExecutionDelayMs)}()", GetIisExecutionDelayMs());

            AnalyzeAppDomain();

            AssemblyResolveEventHandler assemblyResolveEventHandler = CreateAssemblyResolveEventHandler();
            if (assemblyResolveEventHandler == null)
            {
                return;
            }

            Log.Info(LoggingComponentMoniker, "Registering AssemblyResolve handler");

            try
            {
                AppDomain.CurrentDomain.AssemblyResolve += assemblyResolveEventHandler.OnAssemblyResolve;
            }
            catch (Exception ex)
            {
                Log.Error(LoggingComponentMoniker, "Error while registering an AssemblyResolve event handler", ex);
            }

            LogStartingToLoadAssembliesInfo(assemblyResolveEventHandler);

            for (int i = 0; i < assemblyResolveEventHandler.AssemblyNamesToLoad.Count; i++)
            {
                string assemblyName = assemblyResolveEventHandler.AssemblyNamesToLoad[i];

                try
                {
                    LoadAndStartAssembly(assemblyName);
                }
                catch (Exception ex)
                {
                    Log.Error(LoggingComponentMoniker, "Error loading or starting a managed assembly", ex, "assemblyName", assemblyName);
                }
            }
        }

        private static void LogStartingToLoadAssembliesInfo(AssemblyResolveEventHandler assemblyResolveEventHandler)
        {
            var logEntryDetails = new List<object>();
            logEntryDetails.Add("Number of assemblies");
            logEntryDetails.Add(assemblyResolveEventHandler.AssemblyNamesToLoad.Count);
            logEntryDetails.Add("Number of product binaries directories");
            logEntryDetails.Add(assemblyResolveEventHandler.ManagedProductBinariesDirectories.Count);

            for (int i = 0; i < assemblyResolveEventHandler.ManagedProductBinariesDirectories.Count; i++)
            {
                logEntryDetails.Add($"managedProductBinariesDirectories[{i}]");
                logEntryDetails.Add(assemblyResolveEventHandler.ManagedProductBinariesDirectories[i]);
            }

            Log.Info(LoggingComponentMoniker, "Starting to load assemblies", logEntryDetails);
        }

        private static void LoadAndStartAssembly(string assemblyName)
        {
            // We have previously excluded assembly names that are null or white-space.
            assemblyName = assemblyName.Trim();

            Log.Info(LoggingComponentMoniker, "Loading managed assembly", "assemblyName", assemblyName);

            Assembly assembly = Assembly.Load(assemblyName);
            if (assembly == null)
            {
                Log.Error(LoggingComponentMoniker, "Could not load managed assembly", "assemblyName", assemblyName);
                return;
            }

            Exception findEntryPointError = null;
            Type entryPointType = null;
            try
            {
                entryPointType = assembly.GetType(TargetLibraryEntrypointType, throwOnError: false);
            }
            catch (Exception ex)
            {
                findEntryPointError = ex;
            }

            if (entryPointType == null)
            {
                Log.Info(
                        LoggingComponentMoniker,
                        "Assembly was loaded, but entry point was not invoked, bacause it does not contain the entry point type",
                        "assembly.FullName", assembly.FullName,
                        "assembly.Location", assembly.Location,
                        "assembly.CodeBase", assembly.CodeBase,
                        "entryPointType", TargetLibraryEntrypointType,
                        "findEntryPointError", (findEntryPointError == null) ? "None" : $"{findEntryPointError.GetType().Name}: {findEntryPointError.Message}");
                return;
            }

            MethodInfo entryPointMethod = null;
            try
            {
                entryPointMethod = entryPointType.GetMethod(TargetLibraryEntrypointMethod,
                                                            BindingFlags.Public | BindingFlags.Static,
                                                            binder: null,
                                                            types: new Type[0],
                                                            modifiers: null);
            }
            catch (Exception ex)
            {
                findEntryPointError = ex;
            }

            if (entryPointMethod == null)
            {
                Log.Info(
                        LoggingComponentMoniker,
                        "Assembly was loaded, but entry point was not invoked: the entry point type was found, but it does not contain the entry point method (it must be public static)",
                        "assembly.FullName", assembly.FullName,
                        "assembly.Location", assembly.Location,
                        "assembly.CodeBase", assembly.CodeBase,
                        "entryPointType", entryPointType.FullName,
                        "entryPointMethod", TargetLibraryEntrypointMethod,
                        "findEntryPointError", (findEntryPointError == null) ? "None" : $"{findEntryPointError.GetType().Name}: {findEntryPointError.Message}");
                return;
            }

            try
            {
                entryPointMethod.Invoke(obj: null, parameters: null);
            }
            catch (Exception ex)
            {
                Log.Error(
                        LoggingComponentMoniker,
                        "Assembly was loaded and the entry point was invoked; an exception was thrown from the entry point",
                        ex,
                        "assembly.FullName", assembly.FullName,
                        "assembly.Location", assembly.Location,
                        "assembly.CodeBase", assembly.CodeBase,
                        "entryPointType", entryPointType.FullName,
                        "entryPointMethod", entryPointMethod.Name);
                return;
            }

            Log.Info(
                    LoggingComponentMoniker,
                    "Assembly was loaded and the entry point was invoked",
                    "assembly.FullName", assembly.FullName,
                    "assembly.Location", assembly.Location,
                    "assembly.CodeBase", assembly.CodeBase,
                    "entryPointType", entryPointType.FullName,
                    "entryPointMethod", entryPointMethod.Name);
            return;
        }

        private static IReadOnlyList<string> CleanAssemblyNamesToLoad(string[] assemblyNamesToLoad)
        {
            if (assemblyNamesToLoad == null)
            {
                Log.Info(LoggingComponentMoniker, $"Not loading any assemblies ({nameof(assemblyNamesToLoad)} is null). ");
                return null;
            }

            if (assemblyNamesToLoad.Length == 0)
            {
                Log.Info(LoggingComponentMoniker, $"Not loading any assemblies ({nameof(assemblyNamesToLoad)}.{nameof(assemblyNamesToLoad.Length)} is 0). ");
                return null;
            }

            // Check for bad assemblyNamesToLoad entries. We expect the array to be small and entries to be OK.
            // So scrolling multiple times is better then allocating a temp buffer.
            bool someAssemblyNameNeedsCleaning = false;
            int validAssemblyNamesCount = 0;
            for (int pAsmNames = 0; pAsmNames < assemblyNamesToLoad.Length; pAsmNames++)
            {
                if (CleanAssemblyNameToLoad(assemblyNamesToLoad[pAsmNames], out _, out bool asmNameNeedsCleaning))
                {
                    validAssemblyNamesCount++;
                    someAssemblyNameNeedsCleaning = someAssemblyNameNeedsCleaning || asmNameNeedsCleaning;
                }
            }

            if (validAssemblyNamesCount == 0)
            {
                Log.Info(LoggingComponentMoniker, $"Not loading any assemblies. Some assembly names were specified, but they are all null or white-space.");
                return null;
            }

            if (assemblyNamesToLoad.Length == validAssemblyNamesCount && !someAssemblyNameNeedsCleaning)
            {
                return assemblyNamesToLoad;
            }

            string[] validAssemblyNamesToLoad = new string[validAssemblyNamesCount];
            for (int pAsmNames = 0, pValidAsmNames = 0; pAsmNames < assemblyNamesToLoad.Length; pAsmNames++)
            {
                if (CleanAssemblyNameToLoad(assemblyNamesToLoad[pAsmNames], out string cleanAssemblyNameToLoad, out _))
                {
                    validAssemblyNamesToLoad[pValidAsmNames++] = cleanAssemblyNameToLoad;
                }
            }

            return validAssemblyNamesToLoad;
        }

        private static bool CleanAssemblyNameToLoad(string rawAssemblyName, out string cleanAssemblyName, out bool asmNameNeedsCleaning)
        {
            if (String.IsNullOrWhiteSpace(rawAssemblyName))
            {
                cleanAssemblyName = null;
                asmNameNeedsCleaning = true;
                return false;
            }

            const string DllExtension = ".dll";

            cleanAssemblyName = rawAssemblyName.Trim();
            if (cleanAssemblyName.EndsWith(DllExtension, StringComparison.OrdinalIgnoreCase))
            {
                cleanAssemblyName = cleanAssemblyName.Substring(0, cleanAssemblyName.Length - DllExtension.Length);
            }
            else
            {
                if (cleanAssemblyName.Equals(rawAssemblyName, StringComparison.Ordinal))
                {
                    asmNameNeedsCleaning = false;
                    return true;
                }
            }

            if (String.IsNullOrWhiteSpace(cleanAssemblyName))
            {
                cleanAssemblyName = null;
                asmNameNeedsCleaning = true;
                return false;
            }

            asmNameNeedsCleaning = true;
            return true;
        }

        private static IReadOnlyList<string> ResolveManagedProductBinariesDirectories()
        {
            var binaryDirs = new List<string>(capacity: 5);

            GetTracerManagedBinariesDirectories(binaryDirs);
            GetProfilerManagedBinariesDirectories(binaryDirs);

            return binaryDirs;
        }

        private static void GetTracerManagedBinariesDirectories(List<string> binaryDirs)
        {
            // E.g.:
            //  - c:\Program Files\Datadog\.NET Tracer\tracer\net45\
            //  - c:\Program Files\Datadog\.NET Tracer\tracer\netcoreapp3.1\
            //  - ...

            string tracerHomeDirectory = ReadEnvironmentVariable("DD_DOTNET_TRACER_HOME");

            if (String.IsNullOrWhiteSpace(tracerHomeDirectory))
            {
                return;
            }

            string managedBinariesSubdir = GetRuntimeBasedProductBinariesSubdir();
            string managedBinariesDirectory = Path.Combine(tracerHomeDirectory, managedBinariesSubdir);

            if (binaryDirs != null && !String.IsNullOrWhiteSpace(managedBinariesDirectory))
            {
                binaryDirs.Add(managedBinariesDirectory);
            }
        }

        private static void GetProfilerManagedBinariesDirectories(List<string> binaryDirs)
        {
            // E.g.:
            //  - c:\Program Files\Datadog\.NET Tracer\ContinuousProfiler\net45\
            //  - c:\Program Files\Datadog\.NET Tracer\ContinuousProfiler\netcoreapp3.1\
            //  - ...

            string profilerHomeDirectory = ReadEnvironmentVariable("DD_DOTNET_PROFILER_HOME");

            // Be defensive against env var not being set.
            if (String.IsNullOrWhiteSpace(profilerHomeDirectory))
            {
                return;
            }

            string managedBinariesSubdir = GetRuntimeBasedProductBinariesSubdir();
            string managedBinariesDirectory = Path.Combine(profilerHomeDirectory, managedBinariesSubdir);

            if (binaryDirs != null && !String.IsNullOrWhiteSpace(managedBinariesDirectory))
            {
                binaryDirs.Add(managedBinariesDirectory);
            }
        }

        private static string GetRuntimeBasedProductBinariesSubdir()
        {
            return GetRuntimeBasedProductBinariesSubdir(out bool _);
        }

        private static string GetRuntimeBasedProductBinariesSubdir(out bool isCoreFx)
        {
            Assembly objectAssembly = typeof(object).Assembly;
            isCoreFx = (objectAssembly?.FullName?.StartsWith("System.Private.CoreLib") == true);

            string productBinariesSubdir;
            if (isCoreFx)
            {
                // We are running under .NET Core (or .NET 5+).
                // Old versions of .NET core report a major version of 4.
                // The respective binaries are in <HOME>/netstandard2.0/...
                // Newer binaries are in <HOME>/netcoreapp3.1/...
                // This needs to be extended if and when we ship a specific distro for newer .NET versions!

                Version clrVersion = Environment.Version;
                if ((clrVersion.Major == 3 && clrVersion.Minor >= 1) || clrVersion.Major >= 5)
                {
                    productBinariesSubdir = "netcoreapp3.1";
                }
                else
                {
                    productBinariesSubdir = "netstandard2.0";
                }
            }
            else
            {
                // We are running under the (classic) .NET Framework.
                // We currently ship two distributions targeting .NET Framework.
                // We want to use the highest-possible compatible assembly version number.
                // We will try getting the version of mscorlib used.
                // If that version is >= 4.61 then we use the respective distro. Otherwise we use the Net F 4.5 distro.

                try
                {
                    string objectAssemblyFileVersionString = ((AssemblyFileVersionAttribute) objectAssembly.GetCustomAttribute(typeof(AssemblyFileVersionAttribute))).Version;
                    var objectAssemblyVersion = new Version(objectAssemblyFileVersionString);

                    var mscorlib461Version = new Version("4.6.1055.0");

                    productBinariesSubdir = (objectAssemblyVersion < mscorlib461Version) ? "net45" : "net461";
                }
                catch
                {
                    productBinariesSubdir = "net45";
                }
            }

            return productBinariesSubdir;
        }

        private static string ReadEnvironmentVariable(string envVarName)
        {
            try
            {
                return Environment.GetEnvironmentVariable(envVarName);
            }
            catch (Exception ex)
            {
                Log.Error(LoggingComponentMoniker, "Error while reading environment variable", ex, "envVarName", envVarName);
                return null;
            }
        }


        private static void LogToConsoleIfEnabled(string message, Exception ex = null)
        {
            if (UseConsoleLoggingIfFileLoggingFails)
            {
#pragma warning disable IDE0079  // Remove unnecessary suppression: Supresion is necessary for some, but not all compile time settings
#pragma warning disable CS0162   // Unreachable code detected (deliberately using const bool for compile settings)
                Console.WriteLine($"{Environment.NewLine}{LoggingComponentMoniker}: {message}"
                                + (ex == null ? String.Empty : $"{Environment.NewLine}{ex}"));
#pragma warning restore CS0162   // Unreachable code detected
#pragma warning restore IDE0079  // Remove unnecessary suppression
            }
        }

        private void AnalyzeAppDomain()
        {
            AppDomain currAD = AppDomain.CurrentDomain;
            _isDefaultAppDomain = currAD.IsDefaultAppDomain();

            Log.Info(LoggingComponentMoniker,
                     "Will load and start assemblies listed for " + (_isDefaultAppDomain ? "the Default AppDomain" : "Non-default AppDomains") + ".");

            Log.Info(LoggingComponentMoniker,
                     "Listing current AppDomain info",
                     "IsDefaultAppDomain", _isDefaultAppDomain,
                     "Id", currAD.Id,
                     "FriendlyName", currAD.FriendlyName,
#if NETFRAMEWORK
                     "SetupInformation.TargetFrameworkName", currAD.SetupInformation.TargetFrameworkName,
#else
                     "SetupInformation.TargetFrameworkName", "Not available on this .NET version",
#endif
                     "IsFullyTrusted", currAD.IsFullyTrusted,
                     "IsHomogenous", currAD.IsHomogenous,
                     "BaseDirectory", currAD.BaseDirectory,
                     "DynamicDirectory", currAD.DynamicDirectory,
                     "RelativeSearchPath", currAD.RelativeSearchPath,
                     "ShadowCopyFiles", currAD.ShadowCopyFiles);

            Assembly entryAssembly = Assembly.GetEntryAssembly();
            Log.Info(LoggingComponentMoniker,
                     "Listing Entry Assembly info",
                     "FullName", entryAssembly?.FullName,
                     "Location", entryAssembly?.Location);

            TryGetCurrentThread(out int osThreadId, out Thread currentThread);
            Log.Info(LoggingComponentMoniker,
                     "Listing current Thread info",
                     nameof(osThreadId), osThreadId,
                     "IsBackground", currentThread?.IsBackground,
                     "IsThreadPoolThread", currentThread?.IsThreadPoolThread,
                     "ManagedThreadId", currentThread?.ManagedThreadId,
                     "Name", currentThread?.Name);

            if (Log.IsDebugLoggingEnabled)
            {
                string stackTrace = Environment.StackTrace;
                Log.Debug(LoggingComponentMoniker,
                          "Listing invocation Stack Trace",
                          nameof(stackTrace), stackTrace);
            }
        }

        private static bool IsExecuteDelayedEnabled()
        {
            string isDelayEnabledEnvVarString = GetEnvironmentVariable(ExecuteDelayedConstants.IsEnabled_EnvVarName);
            Parse.TryBoolean(isDelayEnabledEnvVarString, ExecuteDelayedConstants.IsEnabled_DefaultVal, out bool isDelayEnabledValue);

            return isDelayEnabledValue;
        }

        private static int GetIisExecutionDelayMs()
        {
            string iisDelayMsEnvVarString = GetEnvironmentVariable(ExecuteDelayedConstants.IisDelayMs_EnvVarName);
            Parse.TryInt32(iisDelayMsEnvVarString, ExecuteDelayedConstants.IisDelayMs_DefaultVal, out int iisExecutionDelayMs);

            return iisExecutionDelayMs;
        }

        private static bool IsAppHostedInIis()
        {
            // Corresponds to the equivalent check in native:
            // https://github.com/DataDog/dd-trace-dotnet/blob/master/tracer/src/Datadog.Trace.ClrProfiler.Native/cor_profiler.cpp#L286-L289

            string processFileName = CurrentProcess.GetMainFileName();
            bool isAppHostedInIis = processFileName.Equals("w3wp.exe", StringComparison.OrdinalIgnoreCase)
                                        || processFileName.Equals("iisexpress.exe", StringComparison.OrdinalIgnoreCase);

            return isAppHostedInIis;
        }

        private static string GetEnvironmentVariable(string endVarName)
        {
            try
            {
                return Environment.GetEnvironmentVariable(endVarName);
            }
            catch
            {
                return null;
            }
        }

        private static bool TryGetCurrentThread(out int osThreadId, out Thread currentThread)
        {
            try
            {
#pragma warning disable CS0618  // GetCurrentThreadId is obsolete but we can still use it for logging purposes (see respective docs)
                osThreadId = AppDomain.GetCurrentThreadId();
#pragma warning restore CS0618  // Type or member is obsolete
                currentThread = Thread.CurrentThread;
                return true;
            }
            catch
            {
                osThreadId = 0;
                currentThread = null;
                return false;
            }
        }

        private AssemblyResolveEventHandler CreateAssemblyResolveEventHandler()
        {
            // Pick the list that we want to load:
            string[] assemblyListToUse = _isDefaultAppDomain
                                                ? _assemblyNamesToLoadIntoDefaultAppDomain
                                                : _assemblyNamesToLoadIntoNonDefaultAppDomains;

            // Set class fields to null so that the arrays can be collected. The "assemblyResolveEventHandler" will encpsulate the data needed.
            _assemblyNamesToLoadIntoDefaultAppDomain = _assemblyNamesToLoadIntoNonDefaultAppDomains = null;

            IReadOnlyList<string> assemblyNamesToLoad = CleanAssemblyNamesToLoad(assemblyListToUse);
            if (assemblyNamesToLoad == null)
            {
                return null;
            }

            IReadOnlyList<string> managedProductBinariesDirectories = ResolveManagedProductBinariesDirectories();

            var assemblyResolveEventHandler = new AssemblyResolveEventHandler(assemblyNamesToLoad, managedProductBinariesDirectories);
            return assemblyResolveEventHandler;
        }
    }
}