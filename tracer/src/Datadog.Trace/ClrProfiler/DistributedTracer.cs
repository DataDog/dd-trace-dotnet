// <copyright file="DistributedTracer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler
{
    /// <summary>
    /// Used to distribute traces across multiple versions of the tracer
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class DistributedTracer
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(DistributedTracer));

        static DistributedTracer()
        {
            try
            {
                var parent = GetDistributedTracer();

                if (parent == null)
                {
                    Log.Information("Building automatic tracer");
                    Instance = new AutomaticTracer();
                }
                else
                {
                    var parentTracer = parent.DuckCast<IAutomaticTracer>();

                    Log.Information("Building manual tracer, connected to {assembly}", parent.GetType().Assembly);

                    Instance = new ManualTracer(parentTracer);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error while building the tracer, fallbacking to automatic");
                Instance = new AutomaticTracer();
            }
        }

        internal static IDistributedTracer Instance { get; }

        /// <summary>
        /// Get the instance of IDistributedTracer. This method will be rewritten by the profiler.
        /// </summary>
        /// <returns>The instance of IDistributedTracer</returns>
        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static object GetDistributedTracer() => Instance;
    }
}
