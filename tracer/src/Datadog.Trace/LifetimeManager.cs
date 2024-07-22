// <copyright file="LifetimeManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Ci;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace
{
    /// <summary>
    /// Used to run hooks on application shutdown
    /// </summary>
    internal class LifetimeManager
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<LifetimeManager>();
        private static LifetimeManager? _instance;
        private readonly ConcurrentQueue<object> _shutdownHooks = new();

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
    }
}
