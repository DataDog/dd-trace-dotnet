using System;
using BenchmarkDotNet.Attributes;
using Datadog.Trace;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.TraceAnnotations;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;

namespace Benchmarks.Trace
{
    [MemoryDiagnoser]
    [BenchmarkAgent6]
    [BenchmarkCategory(Constants.TracerCategory)]
    public class TraceAnnotationsBenchmark
    {
        private static readonly RuntimeMethodHandle MethodHandle;
        private static readonly RuntimeTypeHandle TypeHandle;

        static TraceAnnotationsBenchmark()
        {
            var settings = new TracerSettings
            {
                StartupDiagnosticLogEnabled = false
            };

            Tracer.UnsafeSetTracerInstance(new Tracer(settings, new DummyAgentWriter(), null, null, null));

            var targetMethod = typeof(TraceAnnotationsBenchmark).GetMethod("InstrumentedMethod");
            MethodHandle = targetMethod.MethodHandle;
            TypeHandle = targetMethod.DeclaringType.TypeHandle;

            var bench = new TraceAnnotationsBenchmark();
            bench.RunOnMethodBegin();
        }

        [Benchmark]
        public CallTargetReturn RunOnMethodBegin()
        {
            var state = TraceAnnotationsIntegration.OnMethodBegin<object>(null, MethodHandle, TypeHandle);
            TraceAnnotationsIntegration.OnMethodEnd<object>(null, null, in state);
            return CallTargetReturn.GetDefault();
        }

        public int InstrumentedMethod()
        {
            return 1;
        }
    }
}
