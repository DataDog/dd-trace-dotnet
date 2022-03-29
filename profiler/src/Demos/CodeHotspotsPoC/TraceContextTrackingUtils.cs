// <copyright file="TraceContextTrackingUtils.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Threading;
using Datadog.Monitoring.Configuration;
using Datadog.Monitoring.Profiler.TraceContextTracking;

namespace Datadog.Demos.CodeHotspotsPoC
{
    /// <summary>
    /// Contains CodeHotsots specific code that needs to be used by any assembly that wants to track spans/traces for use by Code Hotspots Profiler.
    /// The Tracer is the primary destination for this.
    /// This class can be copied with minimal modifications (e.g. remove WriteLines, adjust config, etc..).
    /// </summary>
    internal static class TraceContextTrackingUtils
    {
        /// <summary>
        /// The tracer needs to initialize the AsyncLocal holding a span in a manner similar to this.
        /// </summary>
        /// <returns></returns>
        public static AsyncLocal<MockSpan> CreateAsyncSpanPropagator()
        {
            ITraceContextTrackingConfiguration traceContextTrackingConfig = TraceContextTrackingConfigProvider.CreateDefault()
                                                                                             .ApplyReleaseDefaults()
                                                                                             .ApplyEnvironmentVariables()
                                                                                             .CreateImmutableSnapshot();

            TraceContextTrackerFactory.InitializeSingletonInstance(traceContextTrackingConfig);

            if (TraceContextTrackerFactory.SingletonInstance.IsTraceContextTrackingActive)
            {
                return new AsyncLocal<MockSpan>(TraceContextTrackingUtils.OnAsyncLocalValueChanged);
            }
            else
            {
                return new AsyncLocal<MockSpan>();
            }
        }

        /// <summary>
        /// Ths callback is invoked by AsyncLocal each time the values changed for an underlying physical managed tread.
        /// The tracer must use a collback timilar to this.
        /// </summary>
        /// <param name="changeInfo"></param>
        private static void OnAsyncLocalValueChanged(AsyncLocalValueChangedArgs<MockSpan> changeInfo)
        {
            MockSpan currenActiveSpan = changeInfo.CurrentValue;

            Console.WriteLine($"\n* AsyncLocalValueChanged."
                            + $" PrevVal=\"{changeInfo.PreviousValue}\","
                            + $" CurrVal=\"{currenActiveSpan}\","
                            + $" CtxChange={changeInfo.ThreadContextChanged},"
                            + $" ThreadId={Thread.CurrentThread.ManagedThreadId}.");

            if (TraceContextTrackerFactory.SingletonInstance.TryGetOrCreateTraceContextTrackerForCurrentThread(out TraceContextTracker tracker))
            {
                if (currenActiveSpan != null)
                {
                    tracker.TrySetTraceContextInfoForCurrentThread(SpanInfo.ForSpan(currenActiveSpan.TraceId, currenActiveSpan.SpanId));
                }
                else
                {
                    tracker.TrySetTraceContextInfoForCurrentThread(SpanInfo.ForNoSpan());
                }
            }
        }
    }
}
