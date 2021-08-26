using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Datadog.DynamicDiagnosticSourceBindings
{
    /// <summary>
    /// Contains some helpers to <c>DynamicLoader</c> repated to handling the <c>Unloading</c> event for <c>AssemblyLoadContext</c>.
    /// This is mainly becasue <c>DynamicLoader</c> is <c>SecuritySafeCritical</c> and therefore cannot contain <c>async</c> methods.
    /// </summary>
    internal static class AssemblyLoadContextUnloadingEvent
    {
#if NETCOREAPP || NETSTANDARD
        /// <summary>Should be same as in <c>DynamicLoader</c>.</summary>
        private const string LogComponentMoniker = "DynamicAssemblyLoader-DiagnosticSource";

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static Task TrackCompletion(Tuple<WeakReference, string> unloadingInfo)
        {
            // Cause a GC. The AssemblyLoadContext is not fully unloaded until it and everything in it is fully collected.
            GC.Collect();
            GC.WaitForPendingFinalizers();

            // If the ref is no longer alive, then the AssemblyLoadContext has been unloaded. 
            if (!unloadingInfo.Item1.IsAlive)
            {
                return Task.CompletedTask;
            }

            // Kick off an async loop that periodically checks to see if the AssemblyLoadContext has been unloaded.
            return Task.Factory.StartNew(TrackCompletionAsync,
                                         unloadingInfo,
                                         CancellationToken.None,
                                         TaskCreationOptions.None,
                                         TaskScheduler.Default);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static async Task TrackCompletionAsync(object unloadingInfoObj)
        {
            if (unloadingInfoObj == null || !(unloadingInfoObj is Tuple<WeakReference, string> unloadingInfo))
            {
                Log.Error(LogComponentMoniker,
                         $"{nameof(TrackCompletionAsync)} received a param"
                        + " that is null or is NOT a Tuple<WeakReference, string>."
                        + " This is not expected, there must be a bug in this library.");
                return;
            }

            WeakReference unloadingAsmLoadCtxWRef = unloadingInfo.Item1;
            string unloadActionId = unloadingInfo.Item2;

            int unloadWaitIterations = 0;
            while (unloadingAsmLoadCtxWRef.IsAlive && unloadWaitIterations < 10005)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                unloadWaitIterations++;

                if (unloadWaitIterations % 1000 == 0)
                {
                    await Task.Delay(500);

                    if (Log.IsDebugLoggingEnabled)
                    {
                        Log.Debug(LogComponentMoniker,
                                 $"Still waiting for an AssemblyLoadContext to complete unloading after the respective Unloading-event occurred.",
                                  "UnloadActionId", unloadActionId,
                                  "UnloadWaitIterations", unloadWaitIterations,
                                  "CurrentAppDomain.Id", AppDomain.CurrentDomain.Id,
                                  "CurrentAppDomain.FriendlyName", AppDomain.CurrentDomain.FriendlyName,
                                  "CurrentAppDomain.IsDefault", AppDomain.CurrentDomain.IsDefaultAppDomain());
                    }
                }
                else if (unloadWaitIterations % 100 == 0)
                {
                    await Task.Delay(10);
                }
                else if (unloadWaitIterations % 10 == 0)
                {
                    await Task.Delay(1);
                }
            }

            if (unloadingAsmLoadCtxWRef.IsAlive)
            {
                if (Log.IsDebugLoggingEnabled)
                {
                    Log.Debug(LogComponentMoniker,
                             $"AssemblyLoadContext did not complete unloading within a reasonable time after the respective Unloading-event occurred."
                            + " There is likely a reference to something within that Context, and the Context will never be fully unloaded."
                            + " This kind of memory leak is typically an artfact of the user's application. SetupDynamicInvokers() will still"
                            + " be invoked, but it will likely still find the contents of the respective AssemblyLoadContext.",
                              "UnloadActionId", unloadActionId,
                              "UnloadWaitIterations", unloadWaitIterations,
                              "CurrentAppDomain.Id", AppDomain.CurrentDomain.Id,
                              "CurrentAppDomain.FriendlyName", AppDomain.CurrentDomain.FriendlyName,
                              "CurrentAppDomain.IsDefault", AppDomain.CurrentDomain.IsDefaultAppDomain());
                }
            }
        }
#endif
    }
}