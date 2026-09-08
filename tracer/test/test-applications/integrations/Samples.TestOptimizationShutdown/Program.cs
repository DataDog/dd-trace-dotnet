// <copyright file="Program.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Samples.TestOptimizationShutdown;

internal static class Program
{
    public static void Main(string[] args)
    {
        // Load the full tracer without auto-instrumentation so the sample controls initialization order.
        var tracerAssembly = Assembly.LoadFrom(args[0]);
        if (args[1] == "apm-first")
        {
            var tracerType = tracerAssembly.GetType("Datadog.Trace.Tracer", throwOnError: true)!;
            _ = tracerType.GetProperty("Instance")!.GetValue(null);
        }

        var shutdownTrigger = args[2];
        if (shutdownTrigger is "pre-shutdown-failure" or "pre-shutdown-timeout")
        {
            // Register before Test Optimization so a failed hook must not block either
            // the remaining pre-shutdown work or the writer's normal shutdown task.
            var lifetimeType = tracerAssembly.GetType("Datadog.Trace.LifetimeManager", throwOnError: true)!;
            var lifetime = lifetimeType.GetProperty("Instance")!.GetValue(null)!;
            Func<Exception?, Task> shutdownTask = _ =>
            {
                Console.WriteLine("Running pre-shutdown task: " + shutdownTrigger);
                return shutdownTrigger == "pre-shutdown-failure"
                           ? Task.FromException(new InvalidOperationException("Pre-shutdown regression."))
                           : Task.Delay(Timeout.Infinite);
            };
            lifetimeType.GetMethod("AddAsyncPreShutdownTask")!.Invoke(lifetime, [shutdownTask]);
        }

        var optimizationType = tracerAssembly.GetType("Datadog.Trace.Ci.TestOptimization", throwOnError: true)!;
        var optimization = optimizationType.GetProperty("Instance")!.GetValue(null)!;
        optimizationType.GetMethod("Initialize")!.Invoke(optimization, null);

        var sessionType = tracerAssembly.GetType("Datadog.Trace.Ci.TestSession", throwOnError: true)!;
        var getSession = sessionType.GetMethod("GetOrCreate", BindingFlags.Static | BindingFlags.NonPublic)!;
        _ = getSession.Invoke(null, ["shutdown regression", null, "MSTest", null, false]);

        switch (shutdownTrigger)
        {
            case "explicit-close":
                optimizationType.GetMethod("Close")!.Invoke(optimization, null);
                break;
            case "exception":
                var lifetimeType = tracerAssembly.GetType("Datadog.Trace.LifetimeManager", throwOnError: true)!;
                var lifetime = lifetimeType.GetProperty("Instance")!.GetValue(null)!;
                lifetimeType.GetMethod("RunShutdownTasks")!.Invoke(lifetime, [new InvalidOperationException("shutdown regression")]);
                break;
            case "process-exit":
            case "pre-shutdown-failure":
            case "pre-shutdown-timeout":
                // Leave the session open for the real ProcessExit callback.
                break;
            default:
                throw new ArgumentException("Unknown shutdown trigger: " + shutdownTrigger);
        }
    }
}
