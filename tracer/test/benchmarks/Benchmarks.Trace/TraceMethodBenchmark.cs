using System;
using BenchmarkDotNet.Attributes;
using Datadog.Trace;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Custom;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;

namespace Benchmarks.Trace
{
    [MemoryDiagnoser]
    public class TraceMethodBenchmark
    {
        private static readonly RuntimeMethodHandle MethodHandle;
        private static readonly RuntimeTypeHandle TypeHandle;

        static TraceMethodBenchmark()
        {
            var settings = new TracerSettings
            {
                StartupDiagnosticLogEnabled = false
            };

            Tracer.UnsafeSetTracerInstance(new Tracer(settings, new DummyAgentWriter(), null, null, null));

            var targetMethod = typeof(TraceMethodBenchmark).GetMethod("InstrumentedMethod");
            MethodHandle = targetMethod.MethodHandle;
            TypeHandle = targetMethod.DeclaringType.TypeHandle;

            var bench = new TraceMethodBenchmark();
            bench.RunTraceMethodIntegration();
        }

        [Benchmark]
        public CallTargetState RunTraceMethodIntegration()
        {
            CallTargetState returnValue = default;

            for (int i = 0; i < 25; i++)
            {
                returnValue = TraceMethodIntegration.OnMethodBegin<object>(null, MethodHandle, TypeHandle);
            }

            return returnValue;
        }

        public int InstrumentedMethod()
        {
            return 1;
        }


    }
}
