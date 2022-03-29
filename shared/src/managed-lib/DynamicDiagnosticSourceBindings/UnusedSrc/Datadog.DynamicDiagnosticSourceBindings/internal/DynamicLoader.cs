
#if NET5_0 || NETCOREAPP3_1 || NETCOREAPP3_0
    #define SUPPORTED_ASSEMBLYLOADCONTEXT_LOADED_ENUMERATIONS
#endif

using System;
using System.Reflection;
using System.Threading;
using System.Collections.Generic;
using System.IO;

#if NETCOREAPP
    using System.Runtime.Loader;
#endif

using Datadog.Util;

namespace Datadog.DynamicDiagnosticSourceBindings
{
    internal static class DynamicLoader
    {
        private const string LogComonentMoniker = "DynamicAssemblyLoader-DiagnosticSource";

        public const string DiagnosticSourceAssembly_Name = "System.Diagnostics.DiagnosticSource";
        public const string DiagnosticSourceAssembly_Culture = "Culture=neutral";
        public const string DiagnosticSourceAssembly_PublicKeyToken = "PublicKeyToken=cc7b13ffcd2ddd51";

        // Assembly System.Diagnostics.DiagnosticSource version 4.0.2.0 is the first official version of that assembly that contains Activity.
        // (Previous versions contained DiagnosticSource only.)
        // That version was shipped in the System.Diagnostics.DiagnosticSource NuGet version 4.4.0 on 2017-06-28.
        // See https://www.nuget.org/packages/System.Diagnostics.DiagnosticSource/4.4.0
        // It is highly unlikey that an application references an older version of DiagnosticSource.
        // However, if it does, we will not instrument it.
        public static readonly Version DiagnosticSourceAssembly_MinReqVersion = new Version(4, 0, 2, 0);

        public const string ActivityType_FullName = "System.Diagnostics.Activity";

        public enum InitState : int
        {
            NotInitialized = 0,
            Initializing = 1,
            Initialized = 2,
            Error = 3,
        }

        private static int s_InilializationState = (int) InitState.NotInitialized;

        private static PackagedAssemblyLookup s_packagedAssemblies = null;
        private static Assembly s_diagnosticSourceAssembly = null;

        private static DynamicInvokerOld s_invoker = null;

        public static InitState InitializationState { get { return (InitState) s_InilializationState; } }

        public static DynamicInvokerOld Invoker
        {
            get
            {
                if (!EnsureInitialized())
                {
                    throw new InvalidOperationException($"Cannot obtain a dynamic invoker because the {nameof(DynamicLoader)} is not initialized.");
                }

                DynamicInvokerOld invoker = s_invoker;
                if (invoker == null)
                {
                    throw new InvalidOperationException($"Cannot obtain a dynamic invoker. The {nameof(DynamicLoader)} was initialized, but the loader is null.");
                }

                return invoker;
            }
        }

        public static bool EnsureInitialized()
        {
            // Only initialize once:

            if (InitState.Initialized == (InitState) s_InilializationState)
            {
                return true;
            }

            while (true)
            {
                InitState prevInitState = (InitState) Interlocked.CompareExchange(
                                                                        ref s_InilializationState, 
                                                                        (int) InitState.Initializing, 
                                                                        (int) InitState.NotInitialized);
                if (prevInitState == InitState.Error)
                {
                    return false;
                }
                else if (prevInitState == InitState.Initialized)
                {
                    return true;
                }
                else if (prevInitState == InitState.Initializing)
                {
                    // Another thread is initializing the loader.
                    // Perform a short blocking wait.
                    // This is OK, becasue it can only happen early on, before initialization is complete.
                    Thread.Sleep(10);
                }
                else if (prevInitState == InitState.NotInitialized)
                {
                    // That means we won the race and set it to InitState_Initializing. 
                    // Let's do it!
                    break;
                }
                else
                { 
                    throw new InvalidOperationException($"Unexpected inilialization state. {nameof(DynamicLoader)}.{s_InilializationState} = {prevInitState}");
                }
            }

            try
            {
                Log.Debug(LogComonentMoniker, $"Initializing {nameof(DynamicLoader)}.");
                Log.Debug(LogComonentMoniker, $"Runtime version:        {Environment.Version}.");
                Log.Debug(LogComonentMoniker, $"BCL version:            {typeof(object).Assembly.FullName}.");
                Log.Debug(LogComonentMoniker, "");

                bool success = PerformInitialization();

                Log.Debug(LogComonentMoniker, $"Initialization success: {success}.");

                Interlocked.Exchange(ref s_InilializationState, success ? (int) InitState.Initialized : (int) InitState.Error);
                return success;
            }
            catch (Exception ex)
            {
                Log.Error(LogComonentMoniker, ex);
                Interlocked.Exchange(ref s_InilializationState, (int) InitState.Error);
                return false;
            }
        }

        private static bool PerformInitialization()
        {
            // We will now try to load the "right" version of System.Diagnostics.DiagnosticSource.dll
            // (henceforth we'll use the abbreviation DiagnosticSource.dll).
            // Here, "right" is defined as: if the app is using DiagnosticSource.dll we prefer to load that version.
            // Otherwise we prefer the version included with this library, which should be a relatively recent one.

            Assembly diagnosticSourceAssembly = LoadDiagnosticSourceAssembly();
            if (diagnosticSourceAssembly == null)
            {
                return false;
            }

            Type activityType = diagnosticSourceAssembly.GetType(ActivityType_FullName, throwOnError: true);
            if (activityType == null)
            {
                return false;
            }

            var supportedFeatures = new SupportedFeatures()
            {

            };

            s_invoker = new DynamicInvokerOld(supportedFeatures, activityType, null, null);
            return true;
        }

        private static Assembly LoadDiagnosticSourceAssembly()
        {
            // See if DiagnosticSource.dll is already loaded and known to this loader:

            Assembly diagnosticSourceAssembly = s_diagnosticSourceAssembly;
            if (diagnosticSourceAssembly != null)
            {
                return diagnosticSourceAssembly;
            }

            Log.Info(LogComonentMoniker, $"Looking for the \"{DiagnosticSourceAssembly_Name}\" assembly.");

            // Perhaps DiagnosticSource.dll is not yet known to this loader, but it has already been loaded by the application.
            // Let's look for it:

#if SUPPORTED_ASSEMBLYLOADCONTEXT_LOADED_ENUMERATIONS
            IEnumerable<Assembly> loadedAssemblies = AssemblyLoadContext.Default.Assemblies;
#else
            IEnumerable<Assembly> loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
#endif
            if (loadedAssemblies != null)
            {
                foreach (Assembly loadedAssembly in loadedAssemblies)
                {
                    string loadedAssemblyName = loadedAssembly.FullName;
                    if (loadedAssemblyName.StartsWith(DiagnosticSourceAssembly_Name, StringComparison.OrdinalIgnoreCase)
                            && loadedAssemblyName.Contains(DiagnosticSourceAssembly_PublicKeyToken))
                    {
                        if (diagnosticSourceAssembly != null)
                        {
                            throw new InvalidOperationException($"The assembly \"{DiagnosticSourceAssembly_Name}\" is loaded at least twice."
                                                              +  " This is an unsupported condition."
                                                              + $" First instance: [FullName={Format.QuoteOrSpellNull(diagnosticSourceAssembly.FullName)},"
                                                              + $" Location={Format.QuoteOrSpellNull(diagnosticSourceAssembly.Location)}];"
                                                              + $" Second instance: [FullName={Format.QuoteOrSpellNull(loadedAssembly.FullName)},"
                                                              + $" Location={Format.QuoteOrSpellNull(loadedAssembly.Location)}];");
                        }

                        diagnosticSourceAssembly = loadedAssembly;
                    }
                }
            }

            // DiagnosticSource.dll may be loaded, but not into the default AssemblyLoadContext. That is not supported.
            // Let's verify:

#if SUPPORTED_ASSEMBLYLOADCONTEXT_LOADED_ENUMERATIONS
            foreach(AssemblyLoadContext asmLdCtx in AssemblyLoadContext.All)
            {
                if (Object.ReferenceEquals(asmLdCtx, AssemblyLoadContext.Default))
                {
                    continue;    
                }

                foreach (Assembly loadedAssembly in asmLdCtx.Assemblies)
                {
                    string loadedAssemblyName = loadedAssembly.FullName;
                    if (loadedAssemblyName.StartsWith(DiagnosticSourceAssembly_Name, StringComparison.OrdinalIgnoreCase)
                            && loadedAssemblyName.Contains(DiagnosticSourceAssembly_PublicKeyToken))
                    {
                        throw new InvalidOperationException($"The assembly \"{DiagnosticSourceAssembly_Name}\" is loaded into a non-default AssemblyLoadContext."
                                                          +  " This is an unsupported condition."
                                                          + $" AssemblyLoadContext.Name={Format.QuoteOrSpellNull(asmLdCtx.Name)};"
                                                          + $" Loaded assembly: [FullName={Format.QuoteOrSpellNull(loadedAssembly.FullName)},"
                                                          + $" Location={Format.QuoteOrSpellNull(loadedAssembly.Location)}];");
                    }
                }
            }
#endif

            // If we found DiagnosticSource.dll already loaded, we are done:

            if (diagnosticSourceAssembly != null)
            {
                AssemblyName asmName = diagnosticSourceAssembly.GetName();
                if (asmName.Version < DiagnosticSourceAssembly_MinReqVersion)
                {
                    Log.Error(LogComonentMoniker, $"The \"{DiagnosticSourceAssembly_Name}\" assembly is already loaded by the application and the version is too old:"
                       + $" Minimum required version: \"{DiagnosticSourceAssembly_MinReqVersion}\";"
                       + $" Actually loaded assembly: {{Version=\"{asmName.Version}\", Location=\"{diagnosticSourceAssembly.Location}\"}}."
                       +  " Replacing a readily loaded assembly is not supported. Activity-based auto-instrumentation cannot be performed.");

                    diagnosticSourceAssembly = null;
                }
                else
                {
                    Log.Info(LogComonentMoniker, $"The \"{DiagnosticSourceAssembly_Name}\" assembly is already loaded by the application and will be used:"
                                               + $" FullName=\"{diagnosticSourceAssembly.FullName}\", Location=\"{diagnosticSourceAssembly.Location}\".");
                }

                s_diagnosticSourceAssembly = diagnosticSourceAssembly;
                return diagnosticSourceAssembly;
            }

            // Ok, so DiagnosticSource.dll is not already loaded.
            // We need to load it. We will request this by specifying the assembly without the version.
            // The runtime will search the normal asembly resolution paths. If it finds any version, we will use it.
            // This approach is "almost" certain to give us the DiagnosticSource version that would have been loaded
            // by the application if it tries to load the assembly later.
            // (Note there are some unlikely but possible edge cases to still have a version mismatch if the application
            // messes with assembly loading logic in the default load context.)
            // In case that DiagnosticSource is not found in the normal probing paths, we will need to hook up the
            // AssemblyResolve event before we request the load. The event handler will do additional work to fall back
            // to using assembly binaries packaged with this library.

            AppDomain.CurrentDomain.AssemblyResolve += AssemblyResolveEventHandler;

            string diagnosticSourceNameString_NoVersion = $"{DiagnosticSourceAssembly_Name}, {DiagnosticSourceAssembly_Culture}, {DiagnosticSourceAssembly_PublicKeyToken}";
            AssemblyName diagnosticSourceAssemblyName_NoVersion = new AssemblyName(diagnosticSourceNameString_NoVersion);

            Log.Info(LogComonentMoniker, $"The \"{DiagnosticSourceAssembly_Name}\" assembly is not yet loaded by the application."
                                       + $" Performing explicit load by FullName=\"{diagnosticSourceAssemblyName_NoVersion.FullName}\"");

#if NETCOREAPP
            diagnosticSourceAssembly = AssemblyLoadContext.Default.LoadFromAssemblyName(diagnosticSourceAssemblyName_NoVersion);
#else
            diagnosticSourceAssembly = Assembly.Load(diagnosticSourceAssemblyName_NoVersion);
#endif
            if (diagnosticSourceAssembly == null)
            {
                Log.Error(LogComonentMoniker, $"Could not load the \"{DiagnosticSourceAssembly_Name}\" assembly even after advanced assembly resolution logic.");
            }
            else
            {
                AssemblyName asmName = diagnosticSourceAssembly.GetName();
                if (asmName.Version < DiagnosticSourceAssembly_MinReqVersion)
                {
                    // Theoretically we could avoid loading old versions and require at least the min-req version. 
                    // Note however, that this would ONLY work if DiagnosticSource.dll is NOT YET loaded when the DynamicLoader initializes.
                    // If the assembly is ALREADY loaded, then it's too late. If an assembly binary with an old version is in the probing path,
                    // than whether or not the assembly is ALREADY loaded is, essentially, a race condition. For some applications it may result
                    // in different outcomes each time. We do not want to engage in such flaky behaviour also ALWAYS bail out.

                    Log.Error(LogComonentMoniker, $"The \"{DiagnosticSourceAssembly_Name}\" assembly was loaded, but its version too old."
                                                +  " This happens when an old version of the assembly is located in the default assembly probing paths."
                                                +  " A newer version of the assembly is distributed with this library,"
                                                +  " however, the presence of the old assembly version implies that this application requires that partilar version."
                                                + $" Upgrade your application to use a newer version of the \"{DiagnosticSourceAssembly_Name}\" assembly or delete"
                                                +  " the assembly binaries from any assembly probing paths in order to allow this library to inject a newer version."
                                                + $" Minimum required version: \"{DiagnosticSourceAssembly_MinReqVersion}\";"
                                                + $" Actually loaded assembly: {{Version=\"{asmName.Version}\", Location=\"{diagnosticSourceAssembly.Location}\"}}."
                                                +  " Activity-based auto-instrumentation cannot be performed.");

                    diagnosticSourceAssembly = null;
                }
                else
                {
                    Log.Info(LogComonentMoniker, $"The \"{DiagnosticSourceAssembly_Name}\" was loaded:"
                                               + $" FullName=\"{diagnosticSourceAssembly.FullName}\", Location=\"{diagnosticSourceAssembly.Location}\".");
                }
            }

            s_diagnosticSourceAssembly = diagnosticSourceAssembly;
            return diagnosticSourceAssembly;
        }

        private static Assembly AssemblyResolveEventHandler(object sender, ResolveEventArgs args)
        {
            // If the assembly that caused this callback is not one of the assemblies packaged with this library, we do nothing:

            if (args == null || args.Name == null)
            {
                return null;
            }

            PackagedAssemblyLookup packagedAssemblies = GetPackagedAssemblies();

            if (false == packagedAssemblies.TryFind(args.Name, out PackagedAssemblyLookup.Entry packagedAssemblyInfo))
            {
                return null;
            }

            // Ok, we need to do stuff.
            
            // If we already marked this assembly as processed, we are in a recursive call caused by the load request below.
            // That means load failed even after copy and there is nothing else we can do.

            if (packagedAssemblyInfo.IsProcessedFromPackage)
            {
                Log.Error(LogComonentMoniker, $"The assembly \"{args.Name}\" was not found using the normal assembly resolution method."
                                            + $" A fallback assembly binary is included in file \"{packagedAssemblyInfo.AssemblyFilePath}\"."
                                            + $" Copying that file into this application's base directory was attempted, but the assembly still cannot be resolved."
                                            + $" Giving up.");

                return null;
            }

            Log.Info(LogComonentMoniker, $"The assembly \"{args.Name}\" was not found using the normal assembly resolution method."
                                       + $" A fallback assembly binary is included in file \"{packagedAssemblyInfo.AssemblyFilePath}\"."
                                       + $" That file will be now copied into this application's base directory and the loading will be retried.");

            // Validate AppDomain parameter:

            Validate.NotNull(sender, nameof(sender));
            AppDomain senderAppDomain = sender as AppDomain;
            if (senderAppDomain == null)
            {
                throw new ArgumentException($"The specified {nameof(sender)} is expected to be of runtime type {nameof(AppDomain)},"
                                          + $" but the actual type is {sender.GetType().FullName}.");
            }

            CopyFileToBaseDirectory(packagedAssemblyInfo.AssemblyFilePath, senderAppDomain);
            packagedAssemblyInfo.IsProcessedFromPackage = true;

            Log.Info(LogComonentMoniker, $"Assembly binary copied into this application's base directory. Requesting to load assembly \"{packagedAssemblyInfo.AssemblyName}\".");

#if NETCOREAPP
            Assembly resolvedAssembly = AssemblyLoadContext.Default.LoadFromAssemblyName(packagedAssemblyInfo.AssemblyName);
#else
            Assembly resolvedAssembly = Assembly.Load(packagedAssemblyInfo.AssemblyName);
#endif
            return resolvedAssembly;
        }

        private static void CopyFileToBaseDirectory(string srcFilePath, AppDomain appDomain)
        {
            string baseDirectory = appDomain.BaseDirectory;
            string fileName = Path.GetFileName(srcFilePath);
            string dstFilePath = Path.Combine(baseDirectory, fileName);

            Log.Info(LogComonentMoniker, $"Copying file \"{srcFilePath}\" to \"{ dstFilePath}\".");
            try
            {
                File.Copy(srcFilePath, dstFilePath, overwrite: false);
            }
            catch (Exception ex)
            {
                Log.Info(LogComonentMoniker, $"Failed to copy file. Assembly loading will likely fail again. Details: \"{ex.ToString()}\".");
            }
        }

        private static PackagedAssemblyLookup GetPackagedAssemblies()
        {
            PackagedAssemblyLookup packagedAssemblies = s_packagedAssemblies;
            if (packagedAssemblies == null)
            {
                packagedAssemblies = ReadPackagedAssembliesFromDisk();
                packagedAssemblies = Concurrent.TrySetOrGetValue(ref s_packagedAssemblies, packagedAssemblies);
            }

            return packagedAssemblies;
        }

        private static PackagedAssemblyLookup ReadPackagedAssembliesFromDisk()
        { 
            string packagedAssembliesDirectory = GetPackagedAssembliesDirectory();
            var packagedAssemblies = new PackagedAssemblyLookup();

            if (!Directory.Exists(packagedAssembliesDirectory))
            {
                Log.Error(LogComonentMoniker, $"Could not read any packaged assemblies from disk becasue the directory \"{packagedAssembliesDirectory}\" does not exist.");
                return packagedAssemblies;
            }

            foreach (string packagedFilePath in Directory.GetFiles(packagedAssembliesDirectory, "*.dll", SearchOption.AllDirectories))
            {
                if (TryGetAssemblyName(packagedFilePath, out AssemblyName assemblyName))
                {
                    var packagedAssembly = new PackagedAssemblyLookup.Entry(assemblyName, packagedFilePath);
                    packagedAssemblies.Add(packagedAssembly);
                }
            }

            Log.Info(LogComonentMoniker, $"Read {packagedAssemblies.Count} packaged assemblies from \"{packagedAssembliesDirectory}\".");
            return packagedAssemblies;
        }

        internal static bool TryGetAssemblyName(string filePath, out AssemblyName assemblyName)
        {
            if (String.IsNullOrWhiteSpace(filePath))
            {
                assemblyName = null;
                return false;
            }

            try
            {
                assemblyName = AssemblyName.GetAssemblyName(filePath);
                return (assemblyName != null);
            }
            catch
            {
                assemblyName = null;
                return false;
            }
        }

        internal static bool TryParseAssemblyName(string assemblyNameString, out AssemblyName assemblyName)
        {
            if (String.IsNullOrWhiteSpace(assemblyNameString))
            {
                assemblyName = null;
                return false;
            }

            try
            {
                assemblyName = new AssemblyName(assemblyNameString);
                return true;
            }
            catch
            {
                assemblyName = null;
                return false;
            }
        }

        private static string GetPackagedAssembliesDirectory()
        {
            // We will need to place the fallback DiagnosticSource binary with the tracer binaries.
            // The full path should be in some option of environment setting.
            // use a placefolder for now.

            string tracerHomePath = "C:/00/Code/HypotheticalTracerHome/System.Diagnostics.DiagnosticSource-DistributionBinaries/5.0.0-rc.1.20451.14";
            //string tracerHomePath = "c:\\00\\Code\\HypotheticalTracerHome\\System.Diagnostics.DiagnosticSource-DistributionBinaries\\4.4.0\\";

            const string diagnosticSourceVersionSubPath =
#if NET452 || NET451 || NET45
                "net45";
#elif NET462 || NET461 || NET46
                "net46";
#elif NETFRAMEWORK
                "net46";
#elif NETCOREAPP || NET5_0
                "net5.0";
#else
                null;
#endif
            if (diagnosticSourceVersionSubPath == null)
            {
                throw new NotSupportedException("The statically targeted runtime version is not supported by the Tracer.");
            }

            return Path.Combine(tracerHomePath, diagnosticSourceVersionSubPath);
        }
    }
}
