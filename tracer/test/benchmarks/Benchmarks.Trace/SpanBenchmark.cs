extern alias DatadogTraceManual;

using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using Datadog.Trace;
using Datadog.Trace.BenchmarkDotNet;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Extensions;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Proxies;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Tracer;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.ExtensionMethods;
using BindingFlags = System.Reflection.BindingFlags;
using Tracer = Datadog.Trace.Tracer;
using ManualTracer = DatadogTraceManual::Datadog.Trace.Tracer;
using ManualSpanContext = DatadogTraceManual::Datadog.Trace.SpanContext;
using ManualISpan = DatadogTraceManual::Datadog.Trace.ISpan;

namespace Benchmarks.Trace
{
    /// <summary>
    /// Span benchmarks
    /// </summary>
    [MemoryDiagnoser]
    [BenchmarkCategory(Constants.TracerCategory)]
    public class SpanBenchmark
    {
        private Tracer _tracer;
        private ManualTracer _manualTracer;

        [GlobalSetup]
        public void GlobalSetup()
        {
            var settings = TracerSettings.Create(new()
            {
                { ConfigurationKeys.StartupDiagnosticLogEnabled, false },
                { ConfigurationKeys.TraceEnabled, false },
            });

            _tracer = new Tracer(settings, new DummyAgentWriter(), null, null, null);

            // Create the manual integration
            Dictionary<string, object> manualSettings = new();
            CtorIntegration.PopulateSettings(manualSettings, _tracer);

            // Constructor is private, so create using reflection
            _manualTracer = (ManualTracer)typeof(ManualTracer)
                                        .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)[0]
                                        .Invoke(new object[] { _tracer, manualSettings });

            // Warmup
            StartFinishSpan();
        }

        // /// <summary>
        // /// Starts and finishes scope benchmark using the manual instrumentation
        // /// </summary>
        // [Benchmark]
        public void ManualStartFinishScope()
        {
            var manualTracer = _manualTracer.DuckCast<ITracerProxy>();
            var state = StartActiveImplementationIntegration.OnMethodBegin(manualTracer, "operation", (ManualSpanContext)null, null, null, null);
            var nullScope = _manualTracer.StartActive(null!);
            var result = StartActiveImplementationIntegration.OnMethodEnd(manualTracer, nullScope, exception: null, in state);
            using (var scope = result.GetReturnValue())
            {
                var span = scope.Span;
                // TTarget isn't ManualTracer in practice, but it's unused, so it doesn't matter
                SpanExtensionsSetTraceSamplingPriorityIntegration.OnMethodBegin<ManualTracer, ManualISpan>(ref span, SamplingPriority.UserReject);
            }
        }

        /// <summary>
        /// Starts and finishes span benchmark
        /// </summary>
        [Benchmark]
        public void StartFinishSpan()
        {
            var span = _tracer.StartSpan("operation");
            span.SetTraceSamplingPriority(SamplingPriority.UserReject);
            span.Finish();
        }

        /// <summary>
        /// Starts and finishes an scope with span benchmark
        /// </summary>
        [Benchmark]
        public void StartFinishScope()
        {
            using (Scope scope = _tracer.StartActiveInternal("operation"))
            {
                scope.Span.SetTraceSamplingPriority(SamplingPriority.UserReject);
            }
        }
    }
}
