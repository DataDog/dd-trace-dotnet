// <copyright file="ConsoleControlHandler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

#if NETFRAMEWORK
using System;
using System.Runtime.CompilerServices;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Threading;
using Datadog.Trace.Logging;
using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.PlatformHelpers;

/// <summary>
/// Registers a Win32 console control handler for Ctrl+C and Ctrl+Break.
/// </summary>
internal sealed class ConsoleControlHandler : CriticalFinalizerObject
{
    private const int CtrlCEvent = 0;
    private const int CtrlBreakEvent = 1;

    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<ConsoleControlHandler>();

    // Assigned once in the constructor and never replaced, so the delegate (and the native marshalling stub
    // generated for it) stays rooted for exactly as long as the native registration lives.
    private readonly NativeMethods.HandlerRoutine _routine;
    private readonly Action _onControlEvent;
    private readonly TimeSpan _timeout;

    private int _registered;

    [TestingAndPrivateOnly]
    internal ConsoleControlHandler(Action onControlEvent, TimeSpan timeout)
    {
        _onControlEvent = onControlEvent;
        _timeout = timeout;
        _routine = HandleControlEvent;
    }

    ~ConsoleControlHandler()
    {
        // A critical finalizer, because a raw registration is a native function pointer into a managed
        // marshalling stub owned by this AppDomain. If the registration outlives the domain - e.g. a rude unload
        // or an IIS worker being torn down - the next control event walks into a dead domain and takes the process
        // with it. This is the same reason the BCL made ControlCHooker a CriticalFinalizerObject.
        UnregisterCore();
    }

    /// <summary>
    /// Registers a console control handler that runs <paramref name="onControlEvent"/> when the process receives
    /// Ctrl+C or Ctrl+Break.
    /// </summary>
    /// <param name="onControlEvent">The callback to run. It is invoked on a thread pool thread.</param>
    /// <param name="timeout">How long to wait for <paramref name="onControlEvent"/> to complete.</param>
    /// <returns>The registration, or <c>null</c> if the handler could not be registered.</returns>
    public static ConsoleControlHandler? TryRegister(Action onControlEvent, TimeSpan timeout)
    {
        try
        {
            var handler = new ConsoleControlHandler(onControlEvent, timeout);
            return handler.TryRegisterCore() ? handler : null;
        }
        catch (Exception ex)
        {
            // In a partial trust environment the P/Invoke throws a SecurityException, and on a non-Windows
            // runtime it would throw DllNotFoundException. There is no way to flush traces on Ctrl+C without it,
            // so degrade silently and register nothing.
            Log.Warning(ex, "Unable to register a console control handler, so traces will not be flushed when the application receives Ctrl+C");
            return null;
        }
    }

    /// <summary>
    /// Removes the handler from the process's console control handler list. Safe to call more than once.
    /// </summary>
    public void Unregister()
    {
        UnregisterCore();
        GC.SuppressFinalize(this);
    }

    internal bool HandleControlEvent(int controlType)
    {
        try
        {
            if (controlType != CtrlCEvent && controlType != CtrlBreakEvent)
            {
                // Console.CancelKeyPress only ever fires for Ctrl+C and Ctrl+Break, so we ignore
                // CTRL_CLOSE_EVENT, CTRL_LOGOFF_EVENT and CTRL_SHUTDOWN_EVENT to keep exact parity.
                // We could add support for those later if we want (e.g. closing a console window)
                return false;
            }

            // The OS invokes console control handlers on a dedicated thread whose stack is very small on 64-bit
            // Windows, which is why the BCL's ControlCHooker.BreakEvent hands the work to the thread pool and
            // waits for it to finish. Do the same.
            var completed = new ManualResetEventSlim(false);

            if (ThreadPool.QueueUserWorkItem(_ => RunCallback(completed)))
            {
                // Deliberately not disposed: a worker that finishes after we time out would throw
                // ObjectDisposedException on a thread pool thread. LifetimeManager._shutdownComplete
                // is left to the GC for the same reason.
                completed.Wait(_timeout);
            }
            else
            {
                // Couldn't reach the thread pool, so run it here and accept the smaller stack.
                RunCallback(completed);
            }
        }
        catch
        {
            // Never let a managed exception escape into the native caller: that tears down the process,
            // which is the very thing this class exists to prevent.
        }

        // false means "not handled", so the next handler in the list runs and, if nobody handles the event, the
        // OS performs its default action and terminates the process. That matches Console.CancelKeyPress when
        // ConsoleCancelEventArgs.Cancel is not set, which we never did.
        return false;
    }

    // Could throw in partial trust, so no inlining ensures we're caught at the right place
    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool TryRegisterCore()
    {
        if (!NativeMethods.SetConsoleCtrlHandler(_routine, true))
        {
            var error = Marshal.GetLastWin32Error();
            Log.Debug("SetConsoleCtrlHandler failed when registering the console control handler. ErrorCode={ErrorCode}", property: error);
            return false;
        }

        Interlocked.Exchange(ref _registered, 1);
        return true;
    }

    // Runs from a critical finalizer, so it must not allocate, log or throw. The marshalling stub for _routine
    // was already generated by the successful registration, so nothing new needs to be compiled here either.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void UnregisterCore()
    {
        if (Interlocked.Exchange(ref _registered, 0) == 0)
        {
            // Never registered, or already unregistered.
            return;
        }

        try
        {
            // The result is deliberately ignored. SetConsoleCtrlHandler(h, FALSE) fails only when h is not in
            // the list, and the list is reset by exactly the transitions (AllocConsole / FreeConsole /
            // AttachConsole) that would have removed our entry. So "removal failed" implies "nothing was left
            // dangling", and there is nothing to report or retry. .NET Core goes further and skips the call
            // altogether; see https://github.com/dotnet/runtime/blob/d47cfe5617b72752e2e959459e85ed87fcbbeb66/src/libraries/System.Private.CoreLib/src/System/Runtime/InteropServices/PosixSignalRegistration.Windows.cs
            // but we have to do the unregister because multiple app domains can exist in the same process
            NativeMethods.SetConsoleCtrlHandler(_routine, false);
        }
        catch
        {
            // Can only happen in a partial trust environment, which cannot have got this far because
            // _registered is only set once the same P/Invoke has already succeeded.
        }
    }

    private void RunCallback(ManualResetEventSlim completed)
    {
        try
        {
            _onControlEvent();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error handling a console control event.");
        }
        finally
        {
            completed.Set();
        }
    }

    private static class NativeMethods
    {
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal delegate bool HandlerRoutine(int controlType);

        // Also see https://github.com/dotnet/runtime/blob/d47cfe5617b72752e2e959459e85ed87fcbbeb66/src/libraries/Common/src/Interop/Windows/Kernel32/Interop.SetConsoleCtrlHandler.Delegate.cs#L17
        [DllImport("kernel32.dll", CallingConvention = CallingConvention.Winapi, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetConsoleCtrlHandler(HandlerRoutine handlerRoutine, [MarshalAs(UnmanagedType.Bool)] bool add);
    }
}
#endif
