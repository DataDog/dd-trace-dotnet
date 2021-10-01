// <copyright file="LifetimeManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Threading;
using Datadog.Trace.Logging;

namespace Datadog.Trace
{
    /// <summary>
    /// Used to run hooks on application shutdown
    /// </summary>
    internal class LifetimeManager
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<LifetimeManager>();
        private static LifetimeManager _instance;
        private readonly ConcurrentQueue<Action> _shutdownHooks = new();

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
                return LazyInitializer.EnsureInitialized(ref _instance);
            }
        }

        public void AddShutdownTask(Action action)
        {
            _shutdownHooks.Enqueue(action);
        }

        private void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            RunShutdownTasks();
            AppDomain.CurrentDomain.ProcessExit -= CurrentDomain_ProcessExit;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Log.Warning("Application threw an unhandled exception: {Exception}", e.ExceptionObject);
            RunShutdownTasks();
            AppDomain.CurrentDomain.UnhandledException -= CurrentDomain_UnhandledException;
        }

        private void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            RunShutdownTasks();
            Console.CancelKeyPress -= Console_CancelKeyPress;
        }

        private void CurrentDomain_DomainUnload(object sender, EventArgs e)
        {
            RunShutdownTasks();
            AppDomain.CurrentDomain.DomainUnload -= CurrentDomain_DomainUnload;
        }

        private void RunShutdownTasks()
        {
            try
            {
                while (_shutdownHooks.TryDequeue(out var action))
                {
                    action();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error running shutdown hooks");
            }
        }
    }
}
