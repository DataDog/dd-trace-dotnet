// <copyright file="QuartzDiagnosticObserver.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

// Currently to our DiagnosticObserver class isn't available for .NET Framework.
// Our QuartzDiagnosticObserver only works for .NET Framework due to this limitation
// We are purposely avoiding adding a dependency to System.Diagnostics.DiagnosticSource
#if !NETFRAMEWORK
namespace Datadog.Trace.DiagnosticListeners
{
    /// <summary>
    /// Instruments Quartz.NET job scheduler.
    /// <para/>
    /// This observer listens to Quartz diagnostic events to trace job execution,
    /// scheduling, and other Quartz-related operations.
    /// </summary>
    internal sealed class QuartzDiagnosticObserver : DiagnosticObserver
    {
        private const string DiagnosticListenerName = "Quartz";

        protected override string ListenerName => DiagnosticListenerName;

        protected override void OnNext(string eventName, object arg)
        {
            // required for runtime. Listener is made just to listen to the events from the Quartz Library
        }
    }
}

#endif
