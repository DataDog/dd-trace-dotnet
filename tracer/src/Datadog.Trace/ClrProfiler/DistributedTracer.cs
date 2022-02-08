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
        private static readonly Lazy<IDistributedTracer> _lazyInstance = new(() => InitializeDefaultDistributedTracer());
        private static IDistributedTracer _instance = null;

        internal static IDistributedTracer Instance
        {
            get
            {
                if (_instance is null && !_lazyInstance.IsValueCreated)
                {
                    _instance = _lazyInstance.Value;
                }

                return _instance;
            }

            set
            {
                _instance = value;
            }
        }

        /// <summary>
        /// Get the instance of IDistributedTracer. This method will be rewritten by the profiler.
        /// </summary>
        /// <remarks>Don't ever change the return type of this method,
        /// as this would require special handling by the profiler.</remarks>
        /// <returns>The instance of IDistributedTracer</returns>
        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static object GetDistributedTracer() => _instance;

        internal static void SetInstanceOnlyForTests(IDistributedTracer instance)
        {
            _instance = instance;
        }

        private static IDistributedTracer InitializeDefaultDistributedTracer()
        {
            var log = DatadogLogging.GetLoggerFor(typeof(DistributedTracer));

            try
            {
                var parent = GetDistributedTracer();

                if (parent == null)
                {
                    log.Information("Building automatic tracer");
                    return new AutomaticTracer();
                }
                else
                {
                    var parentTracer = parent.DuckCast<IAutomaticTracer>();

                    log.Information("Building manual tracer, connected to {assembly}", parent.GetType().Assembly);
                    return new ManualTracer(parentTracer);
                }
            }
            catch (Exception ex)
            {
                log.Error(ex, "Error while building the tracer, falling back to automatic");
                return new AutomaticTracer();
            }
        }
    }
}
