
#if NET5_0 || NETCOREAPP3_1 || NETCOREAPP3_0
    #define SUPPORTED_ASSEMBLYLOADCONTEXT_LOADED_ENUMERATIONS
#endif

using System;
using System.Reflection;
using System.Threading;
using System.Collections.Generic;
using System.Security;
using System.Threading.Tasks;

#if NETCOREAPP
    using System.Runtime.Loader;
#endif

namespace Datadog.DynamicDiagnosticSourceBindings
{
    [SecuritySafeCritical]
    internal static class DynamicLoader
    {
        private const string LogComponentMoniker = "DynamicAssemblyLoader-DiagnosticSource";

        private static class DiagnosticSourceAssembly
        {
            public const string Name = "System.Diagnostics.DiagnosticSource";
            public const string Culture = "Culture=neutral";
            public const string PublicKeyToken = "PublicKeyToken=cc7b13ffcd2ddd51";

            // Assembly System.Diagnostics.DiagnosticSource version 4.0.2.0 is the first official version of that assembly that contains Activity.
            // (Previous versions contained DiagnosticSource only.)
            // That version was shipped in the System.Diagnostics.DiagnosticSource NuGet version 4.4.0 on 2017-06-28.
            // See https://www.nuget.org/packages/System.Diagnostics.DiagnosticSource/4.4.0
            // It is highly unlikey that an application references an older version of DiagnosticSource.
            // However, if it does, we will not instrument such application.
            public static readonly Version MinReqVersion = new Version(4, 0, 2, 0);
        }

        private static class StubbedTypes
        {
            public  static class FrameworkNames
            {
                public const string DiagnosticSource = "System.Diagnostics.DiagnosticSource";
                public const string DiagnosticListener = "System.Diagnostics.DiagnosticListener";
            }

            public static class VendoredInNames
            {
                public const string DiagnosticSource = "Vendored.System.Diagnostics.DiagnosticSource";
                public const string DiagnosticListener = "Vendored.System.Diagnostics.DiagnosticListener";
            }
        }

        public enum InitState : int
        {
            NotInitialized = 0,
            Initializing = 1,
            Initialized = 2,
            Error = 3,
        }

        private static readonly Assembly s_thisAssembly = typeof(DynamicLoader).Assembly;

        private static readonly object s_setupDynamicInvokersLock = new object();

        private static int s_inilializationState = (int) InitState.NotInitialized;

        private static Assembly s_diagnosticSourceAssemblyInCurrentUse = null;

        public static InitState InitializationState { get { return (InitState) s_inilializationState; } }

        public static bool EnsureInitialized()
        {
            // Only initialize once.

            // If we are initialized, all is good.
            if (InitState.Initialized == (InitState) s_inilializationState)
            {
                return true;
            }

            // Not initialized. Start trying to initialize.
            while (true)
            {
                // IF we were NotInitialized, then switch to Initializing:
                InitState prevInitState = (InitState) Interlocked.CompareExchange(
                                                                        ref s_inilializationState, 
                                                                        (int) InitState.Initializing, 
                                                                        (int) InitState.NotInitialized);

                // We were in an Error state. We are still in Error state now. => Give up.
                if (prevInitState == InitState.Error)
                {
                    return false;
                }

                // We were in Initialized state. We are still in Initialized state. => Success.
                if (prevInitState == InitState.Initialized)
                {
                    return true;
                }

                if (prevInitState == InitState.Initializing)
                {
                    // We were in Initializing state. 
                    // This means that another thread is currently initializing the loader.
                    // We will perform a short blocking wait and then look again by repeating the loop.
                    // Such a small delay is OK. It tends to only happen early on (before initialization is complete) and rare.

                    Thread.Sleep(10);
                }
                else if (prevInitState == InitState.NotInitialized)
                {
                    // We were in NotInitialized state. That means we won the race and changed state to Initializing. 
                    // Exit the loop and proceed with the initialization work.
                    break;
                }
                else
                { 
                    throw new InvalidOperationException($"Unexpected inilialization state. {nameof(DynamicLoader)}.{s_inilializationState} = {prevInitState}");
                }
            }

            // We exited the above loop to here. That means that we won the race and changed state to Initializing. 
            // Let's initialize.

            try
            {
                Log.Info(LogComponentMoniker,
                        $"Started initializing {nameof(DynamicLoader)}.",
                        "Environment.Version", Environment.Version,
                        "BCL Assembly Name", typeof(object).Assembly.FullName,
                        "CurrentAppDomain.Id", AppDomain.CurrentDomain.Id,
                        "CurrentAppDomain.FriendlyName", AppDomain.CurrentDomain.FriendlyName,
                        "CurrentAppDomain.IsDefault", AppDomain.CurrentDomain.IsDefaultAppDomain());

                AppDomain.CurrentDomain.AssemblyLoad += AssemblyLoadEventHandler;
                SetupDynamicInvokers();

                Log.Info(LogComponentMoniker, $"Completed initializing {nameof(DynamicLoader)}.");

                Interlocked.Exchange(ref s_inilializationState, (int) InitState.Initialized);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(LogComponentMoniker, $"Error initializing {nameof(DynamicLoader)}.", ex);

                DynamicInvoker.Current = null;

                Interlocked.Exchange(ref s_inilializationState, (int) InitState.Error);
                return false;
            }
        }

        private static void AssemblyLoadEventHandler(object sender, AssemblyLoadEventArgs args)
        {
            if (args != null && args.LoadedAssembly != null)
            {
                Assembly loadedAssembly = args.LoadedAssembly;
                
                if (AssemblyNameAndTokenMatchDiagnosticSource(loadedAssembly))
                {
                    // The SetupDynamicInvokers(..) routine called from here can cause further assemblies to be loaded, inclusing the DS assembly.
                    // However, it will only execute the branch that causes a DS load if DS is not loaded already. But the fact that
                    // we got here indicated that DS was just loaded. Thus, that branch will not be taken and we are not going to cause recursion.
                    // To protect from non-recursive cuncurrent scenarios, SetupDynamicInvokers(..) takes a lock.
                    if (Log.IsDebugLoggingEnabled)
                    {
                        Log.Debug(LogComponentMoniker,
                                 $"An AssemblyLoad-event occurred, and the loaded assembly matches DiagnosticSource."
                               + $" Invoking {nameof(SetupDynamicInvokers)}.",
                                  "Loaded Assembly FullName", loadedAssembly.FullName,
                                  "Loaded Assembly Location", loadedAssembly.Location,
                                  "Loaded Assembly CodeBase", loadedAssembly.CodeBase,
                                  "CurrentAppDomain.Id", AppDomain.CurrentDomain.Id,
                                  "CurrentAppDomain.FriendlyName", AppDomain.CurrentDomain.FriendlyName,
                                  "CurrentAppDomain.IsDefault", AppDomain.CurrentDomain.IsDefaultAppDomain());
                    }

                    try
                    {
                        SetupDynamicInvokers();

                        Interlocked.Exchange(ref s_inilializationState, (int) InitState.Initialized);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(LogComponentMoniker,
                                 $"{nameof(SetupDynamicInvokers)} was executed because an AssemblyLoad-event occurred, and the loaded assembly"
                               + $" matched DiagnosticSource. That {nameof(SetupDynamicInvokers)}-execution resulted in an error."
                               + $" Any existing dynamic invokers will be invalidated.",
                                  ex,
                                  "AssemblyLoad-event Assembly FullName", loadedAssembly.FullName,
                                  "AssemblyLoad-event Assembly Location", loadedAssembly.Location,
                                  "AssemblyLoad-event Assembly CodeBase", loadedAssembly.CodeBase,
                                  "CurrentAppDomain.Id", AppDomain.CurrentDomain.Id,
                                  "CurrentAppDomain.FriendlyName", AppDomain.CurrentDomain.FriendlyName,
                                  "CurrentAppDomain.IsDefault", AppDomain.CurrentDomain.IsDefaultAppDomain());

                        DynamicInvoker.Current = null;
                        Interlocked.Exchange(ref s_inilializationState, (int) InitState.Error);
                    }
                }
            }
        }

#if SUPPORTED_ASSEMBLYLOADCONTEXT_LOADED_ENUMERATIONS
        private static void AssemblyLoadContextUnloadingEventHandler(AssemblyLoadContext unloadingContext)
        {
            if (unloadingContext == null)
            {
                return;
            }

            string unloadActionId = $"ALC-Unl-{unloadingContext.Name ?? "_"}-{Guid.NewGuid().ToString("N").Substring(22)}";

            if (Log.IsDebugLoggingEnabled)
            {
                Log.Debug(LogComponentMoniker,
                         $"An Unloading-event occurred for a non-default {nameof(AssemblyLoadContext)} that was previously"
                        + " found to conatain an instance on the DiagnosticSource-assembly. Tracking the unload completion.",
                          "UnloadActionId", unloadActionId,
                          "AssemblyLoadContext Name", unloadingContext.Name,
                          "AssemblyLoadContext IsCollectible", unloadingContext.IsCollectible,
                          "CurrentAppDomain.Id", AppDomain.CurrentDomain.Id,
                          "CurrentAppDomain.FriendlyName", AppDomain.CurrentDomain.FriendlyName,
                          "CurrentAppDomain.IsDefault", AppDomain.CurrentDomain.IsDefaultAppDomain());
            }

            // Remove the subscription
            unloadingContext.Unloading -= AssemblyLoadContextUnloadingEventHandler;

            var unloadingContextWRef = new WeakReference(unloadingContext);
            unloadingContext = null;
            AssemblyLoadContextUnloadingEvent.TrackCompletion(Tuple.Create(unloadingContextWRef, unloadActionId))
                                             .ContinueWith(AssemblyLoadContextUnloadedHandler, unloadActionId);
        }

        private static void AssemblyLoadContextUnloadedHandler(Task unloadCompletionTask, object unloadActionIdObj)
        {
            string unloadActionId = unloadActionIdObj?.ToString() ?? "<null>";

            if (Log.IsDebugLoggingEnabled)
            {
                Log.Debug(LogComponentMoniker,
                         $"Completed tracking an Unloading-event occurred for a non-default {nameof(AssemblyLoadContext)} that was previously"
                       +  " found to conatain an instance on the DiagnosticSource-assembly (unloading may or may not have finished - see previous log messages)."
                       + $" Invoking {nameof(SetupDynamicInvokers)}.",
                          "UnloadActionId", unloadActionId,
                          "UnloadCompletionTask.Status", unloadCompletionTask.Status,
                          "CurrentAppDomain.Id", AppDomain.CurrentDomain.Id,
                          "CurrentAppDomain.FriendlyName", AppDomain.CurrentDomain.FriendlyName,
                          "CurrentAppDomain.IsDefault", AppDomain.CurrentDomain.IsDefaultAppDomain());
            }

            try
            {
                SetupDynamicInvokers();

                Interlocked.Exchange(ref s_inilializationState, (int) InitState.Initialized);
            }
            catch (Exception ex)
            {
                Log.Error(LogComponentMoniker,
                         $"{nameof(SetupDynamicInvokers)} was executed becasue an Unloading-event occurred for a non-default"
                       + $" {nameof(AssemblyLoadContext)} that was previously found to conatain an instance on the DiagnosticSource-assembly."
                       + $" That {nameof(SetupDynamicInvokers)}-execution resulted in an error. Any existing dynamic invokers will be invalidated.",
                          ex,
                          "UnloadActionId", unloadActionId,
                          "CurrentAppDomain.Id", AppDomain.CurrentDomain.Id,
                          "CurrentAppDomain.FriendlyName", AppDomain.CurrentDomain.FriendlyName,
                          "CurrentAppDomain.IsDefault", AppDomain.CurrentDomain.IsDefaultAppDomain());

                DynamicInvoker.Current = null;
                Interlocked.Exchange(ref s_inilializationState, (int) InitState.Error);
            }
        }
#endif

        private static void SetupDynamicInvokers()
        {
            lock (s_setupDynamicInvokersLock)
            {
                SetupDynamicInvokersUnderLock(isRecursiveCall: false);
            }
        }

        private static void SetupDynamicInvokersUnderLock(bool isRecursiveCall)
        {
            // @ToDo: describe th algorithm 
            // https://github.com/open-telemetry/opentelemetry-dotnet-instrumentation/issues/13  under "Versioning background" and "DS loading algorithm"

            // Check if DS is loaded, and whether it is loded in a "supported" way
            // (i.e. it is loaded exactly once, into the Default AssemblyLoadContext, and the assembly version is supported).

            FindSuitableLoadedAssembly(out bool isAnyDiagnosticSourceAssemblyLoaded, out bool isSuitableDiagnosticSourceAssemblyLoaded, out Assembly diagnosticSourceAssembly);

            if (isAnyDiagnosticSourceAssemblyLoaded)
            {
                if (isSuitableDiagnosticSourceAssemblyLoaded)
                {
                    // DS is loaded, usable, and diagnosticSourceAssembly points to it.
                    // We need to start using it for all dynamic invocations.

                    UseAssemblyForDynamicInvokers(diagnosticSourceAssembly,
                                                  StubbedTypes.FrameworkNames.DiagnosticSource,
                                                  StubbedTypes.FrameworkNames.DiagnosticListener);

                }
                else
                {
                    // DS is loaded, but something about it is unsopported (we just wrote the details into the Debug Log).
                    // We will fall back to using the vendored-in version that is compiled into this assembly.
                    // This means that users of the dynamic invokers and of the loaded DS versions are not aware of each other and
                    // events and activities do not connect. However, it also means that we are not at risk of negatively affecting
                    // the running application.

                    UseAssemblyForDynamicInvokers(s_thisAssembly,
                                                  StubbedTypes.VendoredInNames.DiagnosticSource,
                                                  StubbedTypes.VendoredInNames.DiagnosticListener);
                }
            }
            else
            {
                // (isAnyDiagnosticSourceAssemblyLoaded == false) => DS is not already loaded.

                if (isRecursiveCall)
                {
                    // (isAnyDiagnosticSourceAssemblyLoaded == false) AND (isRecursiveCall == true).
                    // This means hat we just explicitly tried to load a suitable version of DS, but DS was not loaded at all.
                    // So, DS is not on the normal probing path, and the app is not likely not going to use it at all (nor or later).
                    // We will use the vendored-in fall-back version and it should not cause any problems.

                    UseAssemblyForDynamicInvokers(s_thisAssembly,
                                                  StubbedTypes.VendoredInNames.DiagnosticSource,
                                                  StubbedTypes.VendoredInNames.DiagnosticListener);
                }
                else
                {
                    // (isAnyDiagnosticSourceAssemblyLoaded == false) AND (isRecursiveCall == false).
                    // => DS is not loaded AND we have not just tried loading it already. So:
                    // We need to load it. We will request this by specifying the assembly without the version.
                    // The runtime will search the normal asembly resolution paths. If it finds any version, we will use it.
                    // This approach is "almost" certain to give us the DiagnosticSource version that would have been loaded
                    // by the application if it tries to load the assembly later.
                    // (Note there are some unlikely but possible edge cases to still have a version mismatch if the application
                    // messes with assembly loading logic in the default load context.)

                    string diagnosticSourceNameString_NoVersion = $"{DiagnosticSourceAssembly.Name}, {DiagnosticSourceAssembly.Culture}, {DiagnosticSourceAssembly.PublicKeyToken}";
                    AssemblyName diagnosticSourceAssemblyName_NoVersion = new AssemblyName(diagnosticSourceNameString_NoVersion);

                    if (Log.IsDebugLoggingEnabled)
                    {
                        Log.Debug(LogComponentMoniker,
                                 $"Requesting the runtime to load the DiagnosticSource-assembly by applying the default"
                                + " resolution logic and without specifying any particular assembly version.",
                                  "Requested Assembly Name", diagnosticSourceAssemblyName_NoVersion.FullName,
                                  "CurrentAppDomain.Id", AppDomain.CurrentDomain.Id,
                                  "CurrentAppDomain.FriendlyName", AppDomain.CurrentDomain.FriendlyName,
                                  "CurrentAppDomain.IsDefault", AppDomain.CurrentDomain.IsDefaultAppDomain());
                    }

                    // The subsequent assembly Load will cause AssemblyLoadEventHandler to be invoked.
                    // As a result, we will re-enter this SetupDynamicInvokersUnderLock(..) method indirectly,
                    // so isRecursiveCall will still be False.
                    //  * If the Load will be successful, we will find DS and initialize the invokers when we re-enter.
                    //    So when we eventually return from the Load call below there will be nothing left to do.
                    //  * However, if the Load call will not be successful, then AssemblyLoadEventHandler will not be invoked and we will not re-enter.
                    //    So we need the subsequent SetupDynamicInvokersUnderLock(isRecursiveCall: true) call below to make sure we fall back to the
                    //    vendored version as expected.
                    // Note that the recursive re-entrancy is not causing any issues beyond a one-off re-scan of the loaded assemblies. The 
                    // UseAssemblyForDynamicInvokers makes sure that invokers are not reset if the assembly instance did not actually change.

                    try
                    {
#if NETCOREAPP
                        AssemblyLoadContext.Default.LoadFromAssemblyName(diagnosticSourceAssemblyName_NoVersion);
#else
                        Assembly.Load(diagnosticSourceAssemblyName_NoVersion);
#endif
                    }
                    catch (Exception ex)
                    {
                        // Log as Debug, not as Error, becasue this is an expected condition if the DS assembly is not present.
                        if (Log.IsDebugLoggingEnabled)
                        {
                            Log.Debug(LogComponentMoniker,
                                     $"An exception was thrown while trying to dynamically load the DiagnosticSource-assembly (DS)."
                                    + " This is expected in cases where DS is not present in the resolution paths. The error is loged only in debug mode."
                                    + " A fallback DS will be used.",
                                      "Exception", ex,
                                      "Requested DS Name", diagnosticSourceAssemblyName_NoVersion?.FullName,
                                      "CurrentAppDomain.Id", AppDomain.CurrentDomain.Id,
                                      "CurrentAppDomain.FriendlyName", AppDomain.CurrentDomain.FriendlyName,
                                      "CurrentAppDomain.IsDefault", AppDomain.CurrentDomain.IsDefaultAppDomain());
                        }
                    }

                    SetupDynamicInvokersUnderLock(isRecursiveCall: true);
                }
            }
        }

        private static void UseAssemblyForDynamicInvokers(Assembly diagnosticSourceAssembly, string diagnosticSourceTypeName, string diagnosticListenerTypeName)
        {
            if (Object.ReferenceEquals(s_diagnosticSourceAssemblyInCurrentUse, diagnosticSourceAssembly))
            {
                if (Log.IsDebugLoggingEnabled)
                {
                    Log.Debug(LogComponentMoniker,
                             $"The DiagnosticSource-assembly (DS) instance being set for use by dynamic invokers"
                            + " is the same as the assembly instance already being used. Dynamic invokers will not be reset.",
                              "DS FullName", s_diagnosticSourceAssemblyInCurrentUse?.FullName,
                              "DS Location", s_diagnosticSourceAssemblyInCurrentUse?.Location,
                              "DS CodeBase", s_diagnosticSourceAssemblyInCurrentUse?.CodeBase,
                              "CurrentAppDomain.Id", AppDomain.CurrentDomain.Id,
                              "CurrentAppDomain.FriendlyName", AppDomain.CurrentDomain.FriendlyName,
                              "CurrentAppDomain.IsDefault", AppDomain.CurrentDomain.IsDefaultAppDomain());
                }

                return;
            }

            // We must be under s_setupDynamicInvokersLock when calling this method!
            // So everythign we do should be atomic in respect to re-loading DS and updating invokers.

            Type diagnosticSourceType = diagnosticSourceAssembly.GetType(diagnosticSourceTypeName, throwOnError: true);
            Type diagnosticListenerType = diagnosticSourceAssembly.GetType(diagnosticListenerTypeName, throwOnError: true);

            // Make sure to log type info incl. AssemblyQualifiedName. In theory, types may be forwarded.

            Log.Info(LogComponentMoniker,
                    $"Setting a new instance of the DiagnosticSource-assembly (DS) for use by dynamic invokers."
                   + " Users of existing dynamic invokers may fail, and new stub instances will need to be set up to correct it.",
                     "Previous DS FullName", s_diagnosticSourceAssemblyInCurrentUse?.FullName,
                     "Previous DS Location", s_diagnosticSourceAssemblyInCurrentUse?.Location,
                     "Previous DS CodeBase", s_diagnosticSourceAssemblyInCurrentUse?.CodeBase,
                     "New DS FullName", diagnosticSourceAssembly?.FullName,
                     "New DS Location", diagnosticSourceAssembly?.Location,
                     "New DS CodeBase", diagnosticSourceAssembly?.CodeBase,
                     "DiagnosticSource type", diagnosticSourceType?.AssemblyQualifiedName,
                     "DiagnosticListener type", diagnosticListenerType?.AssemblyQualifiedName,
                     "CurrentAppDomain.Id", AppDomain.CurrentDomain.Id,
                     "CurrentAppDomain.FriendlyName", AppDomain.CurrentDomain.FriendlyName,
                     "CurrentAppDomain.IsDefault", AppDomain.CurrentDomain.IsDefaultAppDomain());

            var newInvoker = new DynamicInvoker(diagnosticSourceAssembly.FullName, diagnosticSourceType, diagnosticListenerType);
            DynamicInvoker.Current = newInvoker;

            s_diagnosticSourceAssemblyInCurrentUse = diagnosticSourceAssembly;
        }

        private static void FindSuitableLoadedAssembly(out bool isAnyDiagnosticSourceAssemblyLoaded, out bool isSuitableDiagnosticSourceAssemblyLoaded, out Assembly suitableAssembly)
        {
            if (Log.IsDebugLoggingEnabled)
            {
                Log.Debug(LogComponentMoniker,
                         $"Scanning all assemblies loaded into the current AppDomain for a suitable version of the"
                       + $" DiagnosticSource-assembly that can be used by the dynamic invokers.",
                          "CurrentAppDomain.Id", AppDomain.CurrentDomain.Id,
                          "CurrentAppDomain.FriendlyName", AppDomain.CurrentDomain.FriendlyName,
                          "CurrentAppDomain.IsDefault", AppDomain.CurrentDomain.IsDefaultAppDomain());
            }

            Assembly diagnosticSourceAssembly = null;

            // Scroll through all loaded assemblies, comparing each one with DS name.
            // If APIs are available, we look only af the default assembly load context. If that case we will
            // later check all other contexts to make sure the DS assembly is not there.

#if SUPPORTED_ASSEMBLYLOADCONTEXT_LOADED_ENUMERATIONS
            IEnumerable<Assembly> loadedAssemblies = AssemblyLoadContext.Default.Assemblies;
#else
            IEnumerable<Assembly> loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
#endif
            if (loadedAssemblies != null)
            {
                foreach (Assembly loadedAssembly in loadedAssemblies)
                {
                    if (AssemblyNameAndTokenMatchDiagnosticSource(loadedAssembly))
                    {
                        if (diagnosticSourceAssembly == null)
                        {
                            // This is the first assembly that matched DS by name. Remember it.

                            diagnosticSourceAssembly = loadedAssembly;
                        }
                        else
                        {
                            // This is NOT the first assembly that matched DS by name. So, DS is loaded at least twice.
                            // This is not supported. => Bail out.

                            if (Log.IsDebugLoggingEnabled)
                            {
                                Log.Debug(LogComponentMoniker,
                                         $"The DiagnosticSource-assembly is loaded two or more times. This is an unsupported condition.",
                                          "1st instance Assembly Name", diagnosticSourceAssembly.FullName,
                                          "1st instance Location", diagnosticSourceAssembly.Location,
                                          "1st instance CodeBase", diagnosticSourceAssembly.CodeBase,
                                          "2nd instance Assembly Name", loadedAssembly.FullName,
                                          "2nd instance Location", loadedAssembly.Location,
                                          "2nd instance CodeBase", loadedAssembly.CodeBase,
                                          "CurrentAppDomain.Id", AppDomain.CurrentDomain.Id,
                                          "CurrentAppDomain.FriendlyName", AppDomain.CurrentDomain.FriendlyName,
                                          "CurrentAppDomain.IsDefault", AppDomain.CurrentDomain.IsDefaultAppDomain());
                            }

                            isAnyDiagnosticSourceAssemblyLoaded = true;
                            isSuitableDiagnosticSourceAssemblyLoaded = false;
                            suitableAssembly = null;
                            return;
                        }
                    }
                }
            }  // if (loadedAssemblies != null)

            // DiagnosticSource.dll may be loaded, but not into the default AssemblyLoadContext. That is not supported.
            // Let's verify.
            // (We verity if we are on a version where the API is available.
            //  Otherwise we must use AppDomain API insted of the AssemblyLoadContext API above to get the full list.
            //  That way we can at least include all contexts into the dont-load-twice check.)

#if SUPPORTED_ASSEMBLYLOADCONTEXT_LOADED_ENUMERATIONS
            foreach (AssemblyLoadContext asmLdCtx in AssemblyLoadContext.All)
            {
                if (Object.ReferenceEquals(asmLdCtx, AssemblyLoadContext.Default))
                {
                    continue;
                }

                foreach (Assembly loadedAssembly in asmLdCtx.Assemblies)
                {
                    if (AssemblyNameAndTokenMatchDiagnosticSource(loadedAssembly))
                    {
                        if (Log.IsDebugLoggingEnabled)
                        {
                            if (diagnosticSourceAssembly != null)
                            {
                                Log.Debug(LogComponentMoniker,
                                         $"The DiagnosticSource-assembly is loaded both, into the default {nameof(AssemblyLoadContext)} (Assembly 1),"
                                       + $" and into at least one non-default {nameof(AssemblyLoadContext)} (Assembly 2). This is an unsupported condition.",
                                          "Assembly 1 Name", diagnosticSourceAssembly.FullName,
                                          "Assembly 1 Location", diagnosticSourceAssembly.Location,
                                          "Assembly 1 CodeBase", diagnosticSourceAssembly.CodeBase,
                                          "Assembly 2 Name", loadedAssembly.FullName,
                                          "Assembly 2 Location", loadedAssembly.Location,
                                          "Assembly 2 CodeBase", loadedAssembly.CodeBase,
                                          "Non-default AssemblyLoadContext Name", asmLdCtx.Name,
                                          "Non-default AssemblyLoadContext IsCollectible", asmLdCtx.IsCollectible,
                                          "CurrentAppDomain.Id", AppDomain.CurrentDomain.Id,
                                          "CurrentAppDomain.FriendlyName", AppDomain.CurrentDomain.FriendlyName,
                                          "CurrentAppDomain.IsDefault", AppDomain.CurrentDomain.IsDefaultAppDomain());
                            }
                            else
                            {
                                Log.Debug(LogComponentMoniker,
                                         $"The DiagnosticSource-assembly is loaded into at least one non-default {nameof(AssemblyLoadContext)}"
                                       + $" (but it is not loaded and into the default {nameof(AssemblyLoadContext)}). This is an unsupported condition.",
                                          "Assembly Name", loadedAssembly.FullName,
                                          "Assembly Location", loadedAssembly.Location,
                                          "Assembly CodeBase", loadedAssembly.CodeBase,
                                          "Non-default AssemblyLoadContext Name", asmLdCtx.Name,
                                          "Non-default AssemblyLoadContext IsCollectible", asmLdCtx.IsCollectible,
                                          "CurrentAppDomain.Id", AppDomain.CurrentDomain.Id,
                                          "CurrentAppDomain.FriendlyName", AppDomain.CurrentDomain.FriendlyName,
                                          "CurrentAppDomain.IsDefault", AppDomain.CurrentDomain.IsDefaultAppDomain());
                            }
                        }

                        asmLdCtx.Unloading += AssemblyLoadContextUnloadingEventHandler;

                        isAnyDiagnosticSourceAssemblyLoaded = true;
                        isSuitableDiagnosticSourceAssemblyLoaded = false;
                        suitableAssembly = null;
                        return;
                    }
                }
            }
#endif  // SUPPORTED_ASSEMBLYLOADCONTEXT_LOADED_ENUMERATIONS

            // Ok, we got here, so we did not detect a problem where DS is loaded more than once or loaded into a non-default context.
            // Is it loaded at all?

            if (diagnosticSourceAssembly == null)
            {
                if (Log.IsDebugLoggingEnabled)
                {
                    Log.Debug(LogComponentMoniker,
                             $"The DiagnosticSource-assembly is currently not loaded.",
                             "CurrentAppDomain.Id", AppDomain.CurrentDomain.Id,
                             "CurrentAppDomain.FriendlyName", AppDomain.CurrentDomain.FriendlyName,
                             "CurrentAppDomain.IsDefault", AppDomain.CurrentDomain.IsDefaultAppDomain());
                }

                isAnyDiagnosticSourceAssemblyLoaded = false;
                isSuitableDiagnosticSourceAssemblyLoaded = false;
                suitableAssembly = null;
                return;
            }

            // DS is loaded exactly once into the default context. Validate the version.

            AssemblyName asmName = diagnosticSourceAssembly.GetName();
            if (asmName == null || asmName.Version < DiagnosticSourceAssembly.MinReqVersion)
            {
                if (Log.IsDebugLoggingEnabled)
                {
                    Log.Debug(LogComponentMoniker,
                             $"The DiagnosticSource-assembly is loaded excatly once"
#if SUPPORTED_ASSEMBLYLOADCONTEXT_LOADED_ENUMERATIONS
                           + $" into a suitable {nameof(AssemblyLoadContext)}"
#endif
                            + ". However, the version of the loaded assembly is older than the minimum supported version (or the version cannot be determined).",
                              "Loaded Assembly Version", diagnosticSourceAssembly.GetName()?.Version,
                              "Loaded Assembly FullName", diagnosticSourceAssembly.FullName,
                              "Loaded Assembly Location", diagnosticSourceAssembly.Location,
                              "Loaded Assembly CodeBase", diagnosticSourceAssembly.CodeBase,
                              "Min Required Version", DiagnosticSourceAssembly.MinReqVersion,
                              "CurrentAppDomain.Id", AppDomain.CurrentDomain.Id,
                              "CurrentAppDomain.FriendlyName", AppDomain.CurrentDomain.FriendlyName,
                              "CurrentAppDomain.IsDefault", AppDomain.CurrentDomain.IsDefaultAppDomain());
                }

                isAnyDiagnosticSourceAssemblyLoaded = false;
                isSuitableDiagnosticSourceAssemblyLoaded = false;
                suitableAssembly = null;
                return;
            }

            // Everythng is as validated.

            if (Log.IsDebugLoggingEnabled)
            {
                Log.Debug(LogComponentMoniker,
                         $"The DiagnosticSource-assembly is loaded excatly once"
#if SUPPORTED_ASSEMBLYLOADCONTEXT_LOADED_ENUMERATIONS
                       + $" into a suitable {nameof(AssemblyLoadContext)}"
#endif
                       + $", and its version is supported by the dynamic invokers.",
                          "Loaded Assembly FullName", diagnosticSourceAssembly.FullName,
                          "Loaded Assembly Location", diagnosticSourceAssembly.Location,
                          "Loaded Assembly CodeBase", diagnosticSourceAssembly.CodeBase,
                          "CurrentAppDomain.Id", AppDomain.CurrentDomain.Id,
                          "CurrentAppDomain.FriendlyName", AppDomain.CurrentDomain.FriendlyName,
                          "CurrentAppDomain.IsDefault", AppDomain.CurrentDomain.IsDefaultAppDomain());
            }

            isAnyDiagnosticSourceAssemblyLoaded = true;
            isSuitableDiagnosticSourceAssemblyLoaded = true;
            suitableAssembly = diagnosticSourceAssembly;
            return;
        }

        private static bool AssemblyNameAndTokenMatchDiagnosticSource(Assembly assembly)
        {
            if (assembly == null)
            {
                return false;
            }

            string assemblyName = assembly.FullName;
            return (assemblyName.StartsWith(DiagnosticSourceAssembly.Name, StringComparison.OrdinalIgnoreCase)
                            && assemblyName.Contains(DiagnosticSourceAssembly.PublicKeyToken));
        }
    }
}
