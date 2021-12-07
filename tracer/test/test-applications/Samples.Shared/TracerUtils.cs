
using System;
using System.Linq;
using System.Reflection;

namespace Samples
{
    public class TracerUtils
    {
        private static readonly Version _manualTracingVersion = new Version("2.255.251.0");

        public static IDisposable StartAutomaticTrace(string operationName)
        {
            // Get the Datadog.Trace.Tracer type from the automatic instrumentation assembly
            Assembly automaticAssembly = System.AppDomain.CurrentDomain.GetAssemblies().Single(asm => asm.GetName().Name.Equals("Datadog.Trace") && asm.GetName().Version < _manualTracingVersion);
            Type tracerType = automaticAssembly.GetType("Datadog.Trace.Tracer");

            // Invoke 'Tracer.Instance'
            var instanceGetMethod = tracerType.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public).GetGetMethod();
            object instance = instanceGetMethod.Invoke(null, new object[] {});

            // Invoke 'public Scope StartActive(string operationName, ISpanContext parent = null, string serviceName = null, DateTimeOffset? startTime = null, bool ignoreActiveScope = false, bool finishOnClose = true)'
            var startActive = tracerType.GetMethod("StartActive");
            object parent = null;
            string serviceName = null;
            DateTimeOffset? startTime = null;
            bool ignoreActiveScope = false;
            bool finishOnClose = true;

            return (IDisposable)startActive.Invoke(instance, new[] { operationName, parent, serviceName, startTime, ignoreActiveScope, finishOnClose });
        }
    }
}
