// <copyright file="LifetimeManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
#if NET6_0_OR_GREATER
using System.Runtime.InteropServices;
#endif
using Datadog.Trace.Ci;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace
{
    /// <summary>
    /// Used to run hooks on application shutdown
    /// </summary>
    internal sealed class LifetimeManager
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<LifetimeManager>();
        private static LifetimeManager? _instance;
        private readonly ConcurrentQueue<object> _shutdownHooks = new();

        // Signaled when RunShutdownTasks finishes. Subsequent callers wait on this
        // instead of returning early, preventing the runtime from tearing down the process prematurely.
        private readonly ManualResetEventSlim _shutdownComplete = new(false);

        // We can be triggered by multiple shutdown paths (ProcessExit, CancelKeyPress, signal handlers, etc).
        // This flag ensures shutdown hooks run at most once.
        private int _shutdownStarted;

#if NET6_0_OR_GREATER
        // .NET 10 breaking change:
        // https://learn.microsoft.com/en-us/dotnet/core/compatibility/core-libraries/10.0/sigterm-signal-handler
        //
        // Starting in .NET 10, the runtime no longer installs default termination signal handlers (SIGTERM on Unix,
        // and Windows equivalents). Without a custom handler, the OS default behavior can terminate the process
        // immediately, skipping managed shutdown events like AppDomain.ProcessExit. We keep these registrations
        // alive for the lifetime of the process to restore the previous behavior.
        private IDisposable? _sigtermRegistration;
        private IDisposable? _sighupRegistration;

        // Prevent multiple concurrent calls to Environment.Exit if multiple termination signals are received.
        private int _terminationExitInitiated;
#endif

        public LifetimeManager()
        {
            // Register callbacks to make sure we flush the traces before exiting
            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
            AppDomain.CurrentDomain.DomainUnload += CurrentDomain_DomainUnload;

            try
            {
                // Registering for the AppDomain.UnhandledException event cannot be called by a security transparent method
                // This will only happen if the Tracer is not run full-trust
                AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Unable to register a callback to the AppDomain.UnhandledException event.");
            }

            try
            {
                // Registering for the cancel key press event requires the System.Security.Permissions.UIPermission
                Console.CancelKeyPress += Console_CancelKeyPress;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Unable to register a callback to the Console.CancelKeyPress event.");
            }

#if NET6_0_OR_GREATER
            // Work around the .NET 10 termination signal change described here:
            // https://learn.microsoft.com/en-us/dotnet/core/compatibility/core-libraries/10.0/sigterm-signal-handler
            TryRegisterTerminationSignalHandlers();
#endif
        }

        public static LifetimeManager Instance
        {
            get
            {
                return LazyInitializer.EnsureInitialized(ref _instance)!;
            }
        }

        public TimeSpan TaskTimeout { get; set; } = TimeSpan.FromSeconds(30);

        public void AddShutdownTask(Action<Exception?> action)
        {
            _shutdownHooks.Enqueue(action);
        }

        public void AddAsyncShutdownTask(Func<Exception?, Task> func)
        {
            _shutdownHooks.Enqueue(func);
        }

        private void CurrentDomain_ProcessExit(object? sender, EventArgs e)
        {
            RunShutdownTasks();
            AppDomain.CurrentDomain.ProcessExit -= CurrentDomain_ProcessExit;
        }

        private void CurrentDomain_UnhandledException(object? sender, UnhandledExceptionEventArgs e)
        {
            if (e == null || e.ExceptionObject is OutOfMemoryException)
            {
                // At this point, the runtime is in a bad state so we give up on exiting gracefully
                return;
            }

            Log.Warning("Application threw an unhandled exception: {Exception}", e.ExceptionObject);
            RunShutdownTasks(e.ExceptionObject as Exception);
            AppDomain.CurrentDomain.UnhandledException -= CurrentDomain_UnhandledException;
        }

        private void Console_CancelKeyPress(object? sender, ConsoleCancelEventArgs e)
        {
            RunShutdownTasks();
            Console.CancelKeyPress -= Console_CancelKeyPress;
        }

        private void CurrentDomain_DomainUnload(object? sender, EventArgs e)
        {
            RunShutdownTasks();
            AppDomain.CurrentDomain.DomainUnload -= CurrentDomain_DomainUnload;
        }

        public void RunShutdownTasks(Exception? exception = null)
        {
            // Ensure shutdown runs once even if multiple events fire.
            if (Interlocked.Exchange(ref _shutdownStarted, 1) != 0)
            {
                // Shutdown already started — wait for it to finish instead of returning immediately.
                // This prevents the runtime from tearing down the process (e.g. after ProcessExit returns)
                // before hooks have completed.
                _shutdownComplete.Wait();
                return;
            }

            // Note: we intentionally do NOT dispose signal registrations here.
            // They must stay alive so that duplicate signals arriving during shutdown
            // are still handled (and canceled) by our handler, preventing the OS from
            // killing the process before hooks finish. They'll be cleaned up by the
            // GC/finalizer when the process exits.

            try
            {
                var current = SynchronizationContext.Current;
                try
                {
                    if (current is not null)
                    {
                        SetSynchronizationContext(null);
                    }

                    while (_shutdownHooks.TryDequeue(out var actionOrFunc))
                    {
                        if (actionOrFunc is Action<Exception?> action)
                        {
                            action(exception);
                        }
                        else if (actionOrFunc is Func<Exception?, Task> func)
                        {
                            AsyncUtil.RunSync(func, exception, (int)TaskTimeout.TotalMilliseconds);
                        }
                        else
                        {
                            Log.Error("Hooks must be of Action<Exception> or Func<Task> types.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error running shutdown hooks");
                }
                finally
                {
                    if (current is not null)
                    {
                        SetSynchronizationContext(current);
                    }

                    DatadogLogging.CloseAndFlush();
                }
            }
            catch
            {
                // Swallow as there's nothing we can with it anyway
            }
            finally
            {
                _shutdownComplete.Set();
            }

            static void SetSynchronizationContext(SynchronizationContext? context)
            {
                if (!AppDomain.CurrentDomain.IsFullyTrusted)
                {
                    // Fix MethodAccessException when the Assembly is loaded as partially trusted.
                    return;
                }

                try
                {
                    SynchronizationContext.SetSynchronizationContext(context);
                }
                catch (MethodAccessException mae)
                {
                    Log.Warning(mae, "Access to security crital method SynchronizationContext.SetSynchronizationContext has failed.");
                }
            }
        }

#if NET6_0_OR_GREATER
        private void TryRegisterTerminationSignalHandlers()
        {
            try
            {
                // We only need this workaround on .NET 10+ runtimes.
                if (Environment.Version.Major < 10)
                {
                    // On .NET <= 9, the runtime provided default termination handlers that result in graceful exit,
                    // so we do not install our own to avoid changing long-standing behavior.
                    return;
                }

                // Register termination handlers to restore the pre-.NET 10 behavior (graceful managed shutdown).
                //
                // Microsoft guidance suggests registering SIGTERM on Unix and SIGTERM+SIGHUP on Windows:
                // https://learn.microsoft.com/en-us/dotnet/core/compatibility/core-libraries/10.0/sigterm-signal-handler
                _sigtermRegistration = PosixSignalRegistration.Create(PosixSignal.SIGTERM, TerminationSignalHandler); // Handle termination (Unix + Windows).

                if (OperatingSystem.IsWindows())
                {
                    // SIGHUP is used as the Windows equivalent for console close/shutdown in the breaking-change guidance.
                    _sighupRegistration = PosixSignalRegistration.Create(PosixSignal.SIGHUP, TerminationSignalHandler); // Handle window close/shutdown equivalents.
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Unable to register termination signal handlers. Graceful shutdown may not run on .NET 10+ termination.");
            }
        }

        private void TerminationSignalHandler(PosixSignalContext context)
        {
            if (Interlocked.Exchange(ref _terminationExitInitiated, 1) != 0)
            {
                // Duplicate signal while shutdown is in progress.
                // Wait for the first handler to finish running shutdown tasks.
                _shutdownComplete.Wait();
                return;
            }

            // Calling Environment.Exit(0); caused an issue in Microsoft Orleans (look https://github.com/DataDog/dd-trace-dotnet/issues/8165)
            // The Posix signals registration mechanism doesn't use a normal MulticastDelegate kind of list; it's using a HashSet<Token> internally.
            // meaning that the call order is not deterministic, creating a flaky behavior between all the handlers.
            // The fact that there's no way to guarantee that we are the last handler means that we cannot force the exit of the process to raise
            // the finalization events calls because that means other handlers will not be called, for that reason we will just proceed with a manual
            // cleanup of our tasks without forcing the exit so other handlers can be executed as well.

            // First signal: run shutdown tasks synchronously before the handler returns.
            // We intentionally do NOT set context.Cancel here — after the handler returns,
            // the runtime/OS will perform the default action (terminate the process with
            // exit code 143), which is the desired behavior once hooks have completed.
            try
            {
                RunShutdownTasks();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to call tracer shutdown with RunShutdownTasks()");
            }
        }
#endif
    }
}
