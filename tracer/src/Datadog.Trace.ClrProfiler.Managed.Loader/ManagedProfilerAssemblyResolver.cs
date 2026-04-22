// <copyright file="ManagedProfilerAssemblyResolver.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.ClrProfiler.Managed.Loader
{
    // This type owns the AppDomain.AssemblyResolve (and .NET Core
    // AssemblyLoadContext.Resolving) callbacks that the tracer registers at
    // startup. It is intentionally a separate static class from Startup so
    // that invoking its static handlers never forces CLR type-initialization
    // of Startup itself.
    //
    // Why that matters: on .NET Framework, if a configBuilder attached to
    // <appSettings> (e.g. AzureAppConfigurationBuilder with useAzureKeyVault
    // and DefaultAzureCredential) issues sync-over-async work during the
    // Startup..cctor chain, the async continuation can run on a ThreadPool
    // thread that needs to resolve a type (Type.GetType), which fires
    // AppDomain.AssemblyResolve. If the handler lives on Startup, the
    // ThreadPool thread has to wait for Startup..cctor to finish; the main
    // thread is already blocked inside that .cctor waiting for the Task,
    // which is waiting for the ThreadPool thread -> classic .cctor deadlock
    // (APMS-19239).
    //
    // Keeping the handler on a class with no non-trivial .cctor means
    // initialization of this type finishes on the main thread before the
    // Task is scheduled, so any ThreadPool thread that later dispatches the
    // handler sees the type as already initialized and runs without blocking.
    internal static partial class ManagedProfilerAssemblyResolver
    {
        // Set by Startup..cctor before subscribing the handler below.
        // An auto-property with no initializer keeps this type beforefieldinit
        // and free of any meaningful class-init work.
        internal static string? ManagedProfilerDirectory { get; set; }
    }
}
