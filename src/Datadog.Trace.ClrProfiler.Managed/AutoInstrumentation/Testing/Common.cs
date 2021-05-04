// <copyright file="Common.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Ci;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing
{
    internal static class Common
    {
        internal static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(Common));
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

        internal static void FlushSpans(IntegrationInfo integrationInfo)
        {
            if (!TestTracer.Settings.IsIntegrationEnabled(integrationInfo))
            {
                return;
            }

            try
            {
                var flushThread = new Thread(() => InternalFlush().GetAwaiter().GetResult());
                flushThread.IsBackground = false;
                flushThread.Name = "FlushThread";
                flushThread.Start();
                flushThread.Join();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception occurred when flushing spans.");
            }

            static async Task InternalFlush()
            {
                try
                {
                    // We have to ensure the flush of the buffer after we finish the tests of an assembly.
                    // For some reason, sometimes when all test are finished none of the callbacks to handling the tracer disposal is triggered.
                    // So the last spans in buffer aren't send to the agent.
                    Log.Debug("Integration flushing spans.");
                    await TestTracer.FlushAsync().ConfigureAwait(false);
                    // The current agent writer FlushAsync method can return inmediately if a payload is being sent (there is buffer lock)
                    // There is not api in the agent writer that guarantees the send has been sucessfully completed.
                    // Until we change the behavior of the agentwriter we should at least wait 2 seconds before returning.
                    Log.Debug("Waiting 2 seconds to flush.");
                    await Task.Delay(2000).ConfigureAwait(false);
                    Log.Debug("Integration flushed.");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Exception occurred when flushing spans.");
                }
            }
        }
    }
}
