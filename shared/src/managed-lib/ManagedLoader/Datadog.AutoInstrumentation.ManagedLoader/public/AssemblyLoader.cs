using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

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

        private bool _isDefaultAppDomain;
        private string[] _assemblyNamesToLoadIntoDefaultAppDomain;
        private string[] _assemblyNamesToLoadIntoNonDefaultAppDomains;

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

        /// <summary>
        /// Instantiates an <c>AssemblyLoader</c> instance with the specified assemblies and executes it.
        /// </summary>
        /// <param name="assemblyNamesToLoadIntoDefaultAppDomain">List of assemblies to load and start if the curret App Domain is the default App Domain.</param>
        /// <param name="assemblyNamesToLoadIntoNonDefaultAppDomains">List of assemblies to load and start if the curret App Domain is the NOT default App Domain.</param>
        public static void Run(string[] assemblyNamesToLoadIntoDefaultAppDomain, string[] assemblyNamesToLoadIntoNonDefaultAppDomains)
        {
            try
            {
                try
                {
                    bool isLoggerSetupDone = false;

                    try
                    {
                        LogConfigurator.SetupLogger();
                        isLoggerSetupDone = true;

                        try
                        {
                            var assemblyLoader = new AssemblyLoader(assemblyNamesToLoadIntoDefaultAppDomain, assemblyNamesToLoadIntoNonDefaultAppDomains);
                            assemblyLoader.Execute();
                        }
                        catch (Exception ex)
                        {
                            // An exception escaped from the loader. We are about to return to the caller, which is likely the IL-generated code in the module cctor.
                            // So all we can do is log the error and swallow it to avoid crashing things.
                            Log.Error(LoggingComponentMoniker, ex);
                        }
                    }
                    finally
                    {
                        CleanSideEffects(isLoggerSetupDone);  // must NOT throw!
                    }
                }
                catch (Exception ex)
                {
                    // We still have an exception, despite the above catch-all. Likely the exception came out of the logger.
                    // We can log it to console it as a backup (if enabled).
#pragma warning disable IDE0079  // Remove unnecessary suppression: Supresion is necessary for some, but not all compile time settings
#pragma warning disable CS0162  // Unreachable code detected (deliberately using const bool for compile settings)
                    if (UseConsoleLoggingIfFileLoggingFails)

                    {
                        Console.WriteLine($"{Environment.NewLine}{LoggingComponentMoniker}: An exception occurred. Assemblies may not be loaded or started."
                                        + $"{Environment.NewLine}{ex}");
                    }
#pragma warning restore CS0162  // Unreachable code detected
#pragma warning restore IDE0079  // Remove unnecessary suppression
                }
            }
            catch
            {
                // We still have an exception passing through the above double-catch-all. Could not even write to console.
                // Our last choice is to let it escape and potentially crash the process or swallow it. We prefer the latter.

            }
        }

        /// <summary>
        /// Loads the assemblied specified for this <c>AssemblyLoader</c> instance and executes their entry point.
        /// </summary>
        public void Execute()
        {
            Log.Info(LoggingComponentMoniker,
                     "Initializing...",
                     "Managed Loader build configuration",
#if DEBUG
                     "Debug"
#else
                     "Release"
#endif
                );

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
            //  - c:\Program Files\Datadog\.NET Tracer\net45\
            //  - c:\Program Files\Datadog\.NET Tracer\netcoreapp3.1\
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
            // Assumed folder structure
            // (support the below options for now; be aware that this may change while we are still in Alpha):
            // OPTION A (SxS Profiler & Tracer):
            //  - c:\Program Files\Datadog\.NET Tracer\                                     <= Native Tracer/Profiler loader binary
            //  - c:\Program Files\Datadog\.NET Tracer\                                     <= Also, native Tracer binaries for Win-x64
            //  - c:\Program Files\Datadog\.NET Tracer\net45\                               <= Managed Tracer binaries for Net Fx 4.5
            //  - c:\Program Files\Datadog\.NET Tracer\netcoreapp3.1\                       <= Managed Tracer binaries for Net Core 3.1 +
            //  - c:\Program Files\Datadog\.NET Tracer\ContinuousProfiler\win-x64\          <= Native Profiler binaries for Win-x64
            //  - c:\Program Files\Datadog\.NET Tracer\ContinuousProfiler\net45\            <= Managed Profiler binaries for Net Fx 4.5
            //  - c:\Program Files\Datadog\.NET Tracer\ContinuousProfiler\netcoreapp3.1\    <= Managed Profiler binaries for Net Core 3.1 +
            //  - ...
            // OPTION B (Profiler only):
            //  - c:\Program Files\Datadog\Some Dir\                                        <= Native Profiler binaries (x86 & x64 .dll)
            //  - c:\Program Files\Datadog\Some Dir\net45\                                  <= Managed Profiler binaries for Net Fx 4.5
            //  - c:\Program Files\Datadog\Some Dir\netcoreapp3.1\                          <= Managed Profiler binaries for Net Core 3.1 +
            //  - ...
            // OPTION C (Debug builds only!):
            //  - Use the relavite path from where our build places the native profiler engine DLL to where the build 
            //    places the managed profiler engine DLL.
            //  - This is for F5-running from within VS.

            string managedBinariesSubdir = GetRuntimeBasedProductBinariesSubdir(out bool isCoreFx);

            string nativeProfilerLoaderAssemblyFile;
            if (isCoreFx)
            {
                nativeProfilerLoaderAssemblyFile = ReadEnvironmentVariable(Environment.Is64BitProcess ? "CORECLR_PROFILER_PATH_64" : "CORECLR_PROFILER_PATH_32");
                nativeProfilerLoaderAssemblyFile = nativeProfilerLoaderAssemblyFile ?? ReadEnvironmentVariable("CORECLR_PROFILER_PATH");
            }
            else
            {
                nativeProfilerLoaderAssemblyFile = ReadEnvironmentVariable(Environment.Is64BitProcess ? "COR_PROFILER_PATH_64" : "COR_PROFILER_PATH_32");
                nativeProfilerLoaderAssemblyFile = nativeProfilerLoaderAssemblyFile ?? ReadEnvironmentVariable("COR_PROFILER_PATH");
            }

            // Be defensive against env var not being set.
            if (String.IsNullOrWhiteSpace(nativeProfilerLoaderAssemblyFile))
            {
                return;
            }

            string nativeProfilerLoaderAssemblyDirectory = Path.GetDirectoryName(nativeProfilerLoaderAssemblyFile);

            {
                // OPTION A from above (SxS with Tracer):

                string tracerHomeDirectory = nativeProfilerLoaderAssemblyDirectory;                             // Shared Tracer/Profiler loader is in Tracer HOME
                string profilerHomeDirectory = Path.Combine(tracerHomeDirectory, "ContinuousProfiler");         // Profiler-HOME is in <Tracer-HOME>/ContinuousProfiler

                string managedBinariesDirectory = Path.Combine(profilerHomeDirectory, managedBinariesSubdir);   // Managed binaries are in <Profiler-HOME>/net-ver-moniker/
                if (binaryDirs != null && !String.IsNullOrWhiteSpace(managedBinariesDirectory))
                {
                    binaryDirs.Add(managedBinariesDirectory);
                }
            }

            {
                // OPTION B from above (Profiler only):

                string profilerHomeDirectory = nativeProfilerLoaderAssemblyDirectory;                           // Profiler-HOME
                string managedBinariesDirectory = Path.Combine(profilerHomeDirectory, managedBinariesSubdir);   // Managed binaries are in <Profiler-HOME>/net-ver-moniker/

                if (binaryDirs != null && !String.IsNullOrWhiteSpace(managedBinariesDirectory))
                {
                    binaryDirs.Add(managedBinariesDirectory);
                }
            }

#if DEBUG
            {
                // OPTION C (Debug builds only!):
                // For debug builds only we also support F5-running from within VS.
                // For that we use the relavite path from where our build places the native profiler engine DLL to where the build
                // places the managed profiler engine DLL.
                const string NativeToManagedRelativePath = @"..\..\..\Debug-AnyCPU\ProfilerEngine\Datadog.AutoInstrumentation.Profiler.Managed\";

                string profilerHomeDirectory = nativeProfilerLoaderAssemblyDirectory;
                string managedBinariesRoot = Path.Combine(profilerHomeDirectory, NativeToManagedRelativePath);

                string managedBinariesRootFull;
                try
                {
                    managedBinariesRootFull = Path.GetFullPath(managedBinariesRoot);
                }
                catch
                {
                    managedBinariesRootFull = managedBinariesRoot;
                }

                string managedBinariesDirectory = Path.Combine(managedBinariesRootFull, managedBinariesSubdir);

                if (binaryDirs != null && !String.IsNullOrWhiteSpace(managedBinariesDirectory))
                {
                    binaryDirs.Add(managedBinariesDirectory);
                }
            }
#endif  // #if DEBUG
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


        /// <summary>
        /// The assembly loader is executed very early in the applicaiton life cycle, earlier that user code normally runs.
        /// This may cause side effects. This method is intended to undo all side effects,
        /// so that the user app is not affected by the presence of the loader.
        /// </summary>
        /// <param name="canUseLog">Whether the Log initialization has been completed and the Log can be used safely.</param>
        private static void CleanSideEffects(bool canUseLog)
        {
            try
            {
                // One method per know side-effect:
                // (each method should catch/log/swallow its exceptions so that other side-effects can be undone)

                ClearAppDomainTargetFrameworkNameCache(canUseLog);
            }
            catch (Exception ex)
            {
                LogErrorOrWriteLine(canUseLog, "An exception occurred while cleaning up side effects.", ex);
            }
        }

        /// <summary>
        /// Clears the AppDomainSetup.TargetFrameworkName cache.
        /// The logger used by this loader uses the Array.Sort(..) API when choosing the log file index.

        /// The behavior of the sort API is Framework version dependent.
        ///
        /// To get the Framework version, it transitively uses the internal AppDomain.GetTargetFrameworkName() API.
        /// The respective value is determined by examining the TargetFrameworkAttribute on Assembly.GetEntryAssembly()
        /// and then caching the result in AppDomainSetup.TargetFrameworkName.
        ///
        /// However, because the Loader runs so early in the app lifecycle, the entry assembly may not yet be initialized (i.e. null).
        /// In such cases, the value cached in AppDomainSetup.TargetFrameworkName is also null, and it does not get updated when the
        /// entry assembly becomes known later.This can break applications that use the target framework name to guide their behavior
        /// (e.g., this is known to break some WCF applications).
        /// 
        /// This method attempts to clear the respective internal cache in AppDomainSetup.
        /// It must be resilient to the internal API being accessed not being present
        /// (in fact, it is known not to be present on some Net Core versions).
        /// </summary>
        private static void ClearAppDomainTargetFrameworkNameCache(bool canUseLog)
        {
            const string ThisCleanupMoniker = "side effects to the AppDomain.GetTargetFrameworkName() cache";

            try
            {
                AppDomain appDomain = AppDomain.CurrentDomain;

                // Failing to clean up may or may NOT be an error:
                // On .NET versions where the AppDomain TargetFrameworkName cache is not present,
                // we expect for some of the reflection in this method to fail.
                // WeakReference will log such cases as Debug lines, not as errors, and bail out gracefully.
                // Actual exceptions, however, are certain to be errors.

                if (appDomain == null)
                {
                    LogDebugOrWriteLine(canUseLog, $"Cannot clean up {ThisCleanupMoniker}: CurrentDomain is null.");
                    return;
                }

                const string FusionStorePropertyName = "FusionStore";
                Type appDomainType = appDomain.GetType();
                PropertyInfo fusionStoreProperty = appDomainType.GetProperty(FusionStorePropertyName, BindingFlags.NonPublic | BindingFlags.Instance);

                if (fusionStoreProperty == null)
                {
                    LogDebugOrWriteLine(canUseLog,
                                        $"Cannot clean up {ThisCleanupMoniker}:"
                                      + $" Did not find non-public property \"{FusionStorePropertyName}\" on type \"{appDomainType.FullName}\""
                                      + $" in assembly \"{appDomainType.Assembly?.FullName}\".");
                    return;
                }

                object appDomainFusionStoreObject = fusionStoreProperty.GetValue(appDomain);

                if (appDomainFusionStoreObject == null)
                {
                    LogDebugOrWriteLine(canUseLog,
                                        $"Cannot clean up {ThisCleanupMoniker}:"
                                      + $" The value of the non-public property \"{FusionStorePropertyName}\" on type \"{appDomainType.FullName}\""
                                      + $" in assembly \"{appDomainType.Assembly?.FullName}\" was retrieved, but found to be null.");
                    return;
                }

                const string CheckedForTargetFrameworkNamePropertyName = "CheckedForTargetFrameworkName";
                Type appDomainSetupType = appDomainFusionStoreObject.GetType();
                PropertyInfo checkedForTargetFrameworkNameProperty = appDomainSetupType.GetProperty(CheckedForTargetFrameworkNamePropertyName,
                                                                                                    BindingFlags.NonPublic | BindingFlags.Instance);

                if (checkedForTargetFrameworkNameProperty == null)
                {
                    LogDebugOrWriteLine(canUseLog,
                                        $"Cannot clean up {ThisCleanupMoniker}:"
                                      + $" Did not find non-public property \"{CheckedForTargetFrameworkNamePropertyName}\" on type \"{appDomainSetupType.FullName}\""
                                      + $" in assembly \"{appDomainType.Assembly?.FullName}\".");
                    return;
                }

                checkedForTargetFrameworkNameProperty.SetValue(appDomainFusionStoreObject, false);

                LogDebugOrWriteLine(canUseLog, $"Completed clening up {ThisCleanupMoniker}.");
            }
            catch (Exception ex)
            {
                LogErrorOrWriteLine(canUseLog, $"An exception occurred while cleaning up {ThisCleanupMoniker}.", ex);
            }
        }

        private static void LogErrorOrWriteLine(bool canUseLog, string message, Exception ex = null)
        {
            if (canUseLog)
            {
                Log.Error(LoggingComponentMoniker, message, ex);
            }
            else if (UseConsoleLoggingIfFileLoggingFails)
            {
#pragma warning disable IDE0079  // Remove unnecessary suppression: Supresion is necessary for some, but not all compile time settings
#pragma warning disable CS0162  // Unreachable code detected (deliberately using const bool for compile settings)
                Console.WriteLine($"{Environment.NewLine}{LoggingComponentMoniker}: {message}"
                                + (ex == null ? "" : $"{Environment.NewLine}{ex}"));
#pragma warning restore CS0162  // Unreachable code detected
#pragma warning restore IDE0079  // Remove unnecessary suppression
            }
        }

        private static void LogDebugOrWriteLine(bool canUseLog, string message)
        {
            if (canUseLog)
            {
                Log.Debug(LoggingComponentMoniker, message);
            }
            else if (UseConsoleLoggingIfFileLoggingFails)
            {
#pragma warning disable IDE0079  // Remove unnecessary suppression: Supresion is necessary for some, but not all compile time settings
#pragma warning disable CS0162  // Unreachable code detected (deliberately using const bool for compile settings)
                Console.WriteLine($"{Environment.NewLine}{LoggingComponentMoniker}: {message}");
#pragma warning restore CS0162  // Unreachable code detected
#pragma warning restore IDE0079  // Remove unnecessary suppression
            }
        }

        private void AnalyzeAppDomain()
        {
            AppDomain currAD = AppDomain.CurrentDomain;
            _isDefaultAppDomain = currAD.IsDefaultAppDomain();

            Log.Info(LoggingComponentMoniker,
                    "Will load and start assemblies listed for " + (_isDefaultAppDomain ? "the Default AppDomain" : "Non-default AppDomains") + ".");

            Log.Info(
                    LoggingComponentMoniker,
                    "Listing current AppDomain info",
                    "IsDefaultAppDomain",
                    _isDefaultAppDomain,
                    "Id",
                    currAD.Id,
                    "FriendlyName",
                    currAD.FriendlyName,
                    "IsFullyTrusted",
                    currAD.IsFullyTrusted,
                    "IsHomogenous",
                    currAD.IsHomogenous,
                    "BaseDirectory",
                    currAD.BaseDirectory,
                    "DynamicDirectory",
                    currAD.DynamicDirectory,
                    "RelativeSearchPath",
                    currAD.RelativeSearchPath,
                    "ShadowCopyFiles",
                    currAD.ShadowCopyFiles);

            Assembly entryAssembly = Assembly.GetEntryAssembly();
            Log.Info(
                    LoggingComponentMoniker,
                    "Listing Entry Assembly info",
                    "FullName",
                    entryAssembly?.FullName,
                    "Location",
                    entryAssembly?.Location);
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
