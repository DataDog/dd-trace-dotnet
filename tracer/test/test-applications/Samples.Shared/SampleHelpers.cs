using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Samples
{
    public class SampleHelpers
    {
        private static readonly Type NativeMethodsType = Type.GetType("Datadog.Trace.ClrProfiler.NativeMethods, Datadog.Trace");
        private static readonly Type TracerType = Type.GetType("Datadog.Trace.Tracer, Datadog.Trace");
        private static readonly Type ScopeType = Type.GetType("Datadog.Trace.Scope, Datadog.Trace");
        private static readonly Type SpanType = Type.GetType("Datadog.Trace.Span, Datadog.Trace");
        private static readonly Type CorrelationIdentifierType = Type.GetType("Datadog.Trace.CorrelationIdentifier, Datadog.Trace");
        private static readonly MethodInfo GetTracerInstance = TracerType.GetProperty("Instance").GetMethod;
        private static readonly MethodInfo StartActiveMethod = TracerType.GetMethod("StartActive", types: new[] { typeof(string) });
        private static readonly MethodInfo ForceFlushAsyncMethod = TracerType.GetMethod("ForceFlushAsync", BindingFlags.Public | BindingFlags.Instance);
        private static readonly MethodInfo ActiveScopeProperty = TracerType.GetProperty("ActiveScope").GetMethod;
        private static readonly MethodInfo SpanProperty = ScopeType.GetProperty("Span", BindingFlags.NonPublic | BindingFlags.Instance).GetMethod;
        private static readonly MethodInfo CorrelationIdentifierTraceIdProperty = CorrelationIdentifierType.GetProperty("TraceId", BindingFlags.Public | BindingFlags.Static).GetMethod;
        private static readonly MethodInfo SetResourceNameProperty = SpanType.GetProperty("ResourceName", BindingFlags.NonPublic | BindingFlags.Instance).SetMethod;
        private static readonly MethodInfo SetTagMethod = SpanType.GetMethod("SetTag", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly MethodInfo SetExceptionMethod = SpanType.GetMethod("SetException", BindingFlags.NonPublic | BindingFlags.Instance);

        public static bool IsProfilerAttached()
        {
            if(NativeMethodsType is null)
            {
                return false;
            }

            try
            {
                MethodInfo profilerAttachedMethodInfo = NativeMethodsType.GetMethod("IsProfilerAttached");
                return (bool)profilerAttachedMethodInfo.Invoke(null, null);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            return false;
        }

        public static string GetTracerAssemblyLocation()
        {
            return NativeMethodsType?.Assembly.Location ?? "(none)";
        }

        public static void RunShutDownTasks(object caller)
        {
            var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();

            foreach (var assembly in assemblies)
            {
                foreach (var type in assembly.DefinedTypes)
                {
                    if (type.Namespace == "Coverlet.Core.Instrumentation.Tracker")
                    {
                        var unloadModuleMethod = type.GetMethod("UnloadModule", BindingFlags.Public | BindingFlags.Static);
                        unloadModuleMethod.Invoke(null, new object[] { caller, EventArgs.Empty });
                    }
                }
            }
        }

        public static IDisposable CreateScope(string operationName)
        {
            var tracer = GetTracerInstance.Invoke(null, Array.Empty<object>());
            return (IDisposable) StartActiveMethod.Invoke(tracer, new object[] { operationName });
        }
        public static Task ForceTracerFlushAsync()
        {
            var tracer = GetTracerInstance.Invoke(null, Array.Empty<object>());
            return (Task)ForceFlushAsyncMethod.Invoke(tracer, Array.Empty<object>());
        }

        public static IDisposable GetActiveScope()
        {
            var tracer = GetTracerInstance.Invoke(null, Array.Empty<object>());
            return (IDisposable) ActiveScopeProperty.Invoke(tracer, Array.Empty<object>());
        }

        public static ulong GetCorrelationIdentifierTraceId()
        {
            return (ulong)CorrelationIdentifierTraceIdProperty.Invoke(null, Array.Empty<object>());
        }

        public static void TrySetResourceName(object scope, string resourceName)
        {
            var span = SpanProperty.Invoke(scope, Array.Empty<object>());
            SetResourceNameProperty.Invoke(span, new object[] { resourceName });
        }

        public static void TrySetTag(object scope, string key, string value)
        {
            var span = SpanProperty.Invoke(scope, Array.Empty<object>());
            SetTagMethod.Invoke(span, new object[] { key, value });
        }

        public static void TrySetExceptionOnActiveScope(Exception exception)
        {
            var tracer = GetTracerInstance.Invoke(null, Array.Empty<object>());
            var scope = ActiveScopeProperty.Invoke(tracer, Array.Empty<object>());
            if (scope is null)
            {
                return;
            }

            var span = SpanProperty.Invoke(scope, Array.Empty<object>());
            SetExceptionMethod.Invoke(span, new object[] { new Exception() });
        }

        public static IEnumerable<KeyValuePair<string,string>> GetDatadogEnvironmentVariables()
        {
            var prefixes = new[] { "COR_", "CORECLR_", "DD_", "DATADOG_" };

            var envVars = from envVar in Environment.GetEnvironmentVariables().Cast<DictionaryEntry>()
                          from prefix in prefixes
                          let key = (envVar.Key as string)?.ToUpperInvariant()
                          let value = envVar.Value as string
                          where key.StartsWith(prefix)
                          orderby key
                          select new KeyValuePair<string, string>(key, value);

            return envVars.ToList();
        }
    }
}
