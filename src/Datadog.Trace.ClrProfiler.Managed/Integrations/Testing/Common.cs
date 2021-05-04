// <copyright file="Common.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Ci;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.Integrations.Testing
{
    internal static class Common
    {
        private static object _padLock = new object();
        private static Tracer _testTracer = null;

        static Common()
        {
            // Preload environment variables.
            CIEnvironmentValues.DecorateSpan(null);
        }

        internal static Tracer TestTracer
        {
            get
            {
                if (_testTracer is null)
                {
                    lock (_padLock)
                    {
                        if (_testTracer is null)
                        {
                            var settings = TracerSettings.FromDefaultSources();
                            settings.TraceBufferSize = 1024 * 1024 * 45; // slightly lower than the 50mb payload agent limit.

                            _testTracer = new Tracer(settings);
                            Tracer.Instance = _testTracer;
                        }
                    }
                }

                return _testTracer;
            }
        }
    }
}
