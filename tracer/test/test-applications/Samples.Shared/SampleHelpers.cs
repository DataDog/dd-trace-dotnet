using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Samples
{
    public class SampleHelpers
    {
        private static readonly Type InstrumentationType = Type.GetType("Datadog.Trace.ClrProfiler.Instrumentation, Datadog.Trace");
        private static readonly Type NativeMethodsType = Type.GetType("Datadog.Trace.ClrProfiler.NativeMethods, Datadog.Trace");
        private static readonly Type TracerType = Type.GetType("Datadog.Trace.Tracer, Datadog.Trace");
        private static readonly Type ScopeType = Type.GetType("Datadog.Trace.Scope, Datadog.Trace");
        private static readonly Type SpanType = Type.GetType("Datadog.Trace.Span, Datadog.Trace");
        private static readonly Type SpanContextExtractorType = Type.GetType("Datadog.Trace.SpanContextExtractor, Datadog.Trace");
        private static readonly Type CorrelationIdentifierType = Type.GetType("Datadog.Trace.CorrelationIdentifier, Datadog.Trace");
        private static readonly Type SpanCreationSettingsType = Type.GetType("Datadog.Trace.SpanCreationSettings, Datadog.Trace");
        private static readonly Type SpanContextType = Type.GetType("Datadog.Trace.SpanContext, Datadog.Trace");
        private static readonly Type TracerSettingsType = Type.GetType("Datadog.Trace.TracerSettings, Datadog.Trace.Configuration");
        private static readonly Type TracerConstantsType = Type.GetType("Datadog.Trace.TracerConstants, Datadog.Trace");
        private static readonly MethodInfo GetNativeTracerVersionMethod = InstrumentationType?.GetMethod("GetNativeTracerVersion");
        private static readonly MethodInfo GetTracerInstance = TracerType?.GetProperty("Instance")?.GetMethod;
        private static readonly MethodInfo StartActiveMethod = TracerType?.GetMethod("StartActive", types: new[] { typeof(string) });
        private static readonly MethodInfo StartActiveWithContextMethod;
        private static readonly MethodInfo ExtractMethod = SpanContextExtractorType?.GetMethod("Extract");
        private static readonly MethodInfo SetParent = SpanCreationSettingsType?.GetProperty("Parent")?.SetMethod;
        private static readonly MethodInfo ForceFlushAsyncMethod = TracerType?.GetMethod("ForceFlushAsync", BindingFlags.Public | BindingFlags.Instance);
        private static readonly MethodInfo ActiveScopeProperty = TracerType?.GetProperty("ActiveScope")?.GetMethod;
        private static readonly MethodInfo ConfigureMethod = TracerType?.GetMethod("Configure");
        private static readonly MethodInfo TraceIdProperty = SpanContextType?.GetProperty("TraceId")?.GetMethod;
        private static readonly MethodInfo SpanIdProperty = SpanContextType?.GetProperty("SpanId")?.GetMethod;
        private static readonly MethodInfo SpanProperty = ScopeType?.GetProperty("Span", BindingFlags.NonPublic | BindingFlags.Instance)?.GetMethod;
        private static readonly MethodInfo SpanContextProperty = SpanType?.GetProperty("Context", BindingFlags.NonPublic | BindingFlags.Instance)?.GetMethod;
        private static readonly MethodInfo CorrelationIdentifierTraceIdProperty = CorrelationIdentifierType?.GetProperty("TraceId", BindingFlags.Public | BindingFlags.Static)?.GetMethod;
        private static readonly MethodInfo SetResourceNameProperty = SpanType?.GetProperty("ResourceName", BindingFlags.NonPublic | BindingFlags.Instance)?.SetMethod;
        private static readonly MethodInfo SetTagMethod = SpanType?.GetMethod("SetTag", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly MethodInfo SetExceptionMethod = SpanType?.GetMethod("SetException", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly MethodInfo FromDefaultSourcesMethod = TracerSettingsType?.GetMethod("FromDefaultSources", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly MethodInfo SetService = TracerSettingsType?.GetProperty("Service")?.SetMethod;
        private static readonly FieldInfo TracerThreePartVersionField = TracerConstantsType?.GetField("ThreePartVersion");


        static SampleHelpers()
        {
            if (TracerType is null)
            {
                Console.WriteLine("*** [Warning] SampleHelpers.TracerType is null so you may experience missing spans. Ensure automatic instrumentation is correctly enabled for this application to make sure spans are generated. ***");
            }
            else
            {
                if (SpanCreationSettingsType is null)
                {
                    return;
                }

                StartActiveWithContextMethod = TracerType?.GetMethod("StartActive", types: new[] { typeof(string), SpanCreationSettingsType });
            }
        }

        public static void ConfigureTracer(string serviceName)
        {
            if (TracerSettingsType is null || GetTracerInstance is null || ConfigureMethod is null || FromDefaultSourcesMethod is null)
            {
                return;
            }
            var tracerSettings = FromDefaultSourcesMethod.Invoke(null, Array.Empty<object>());
            SetService.Invoke(tracerSettings, new object[] { serviceName });

            var tracer = GetTracerInstance.Invoke(null, Array.Empty<object>());
            ConfigureMethod.Invoke(tracer, new object[] { tracerSettings });
        }

        public static bool IsProfilerAttached()
        {
            if(NativeMethodsType is null)
            {
                return false;
            }

            try
            {
                var profilerAttachedMethodInfo = NativeMethodsType.GetMethod("IsProfilerAttached");
                return (bool) profilerAttachedMethodInfo.Invoke(null, null);
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

        public static string GetNativeTracerVersion()
        {
            try
            {
                return (string)GetNativeTracerVersionMethod.Invoke(null, Array.Empty<object>()) ?? "None";
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return "None";
            }
        }

        public static string GetManagedTracerVersion()
        {
            try
            {
                return (string) TracerThreePartVersionField.GetValue(null);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return "None";
            }
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
            if (GetTracerInstance is null || StartActiveMethod is null)
            {
                return new NoOpDisposable();
            }

            var tracer = GetTracerInstance.Invoke(null, Array.Empty<object>());
            return (IDisposable) StartActiveMethod.Invoke(tracer, new object[] { operationName });
        }

        public static IDisposable CreateScopeWithPropagation<TCarrier>(string operationName, TCarrier carrier, Func<TCarrier, string, IEnumerable<string>> getter)
        {
            if (GetTracerInstance is null || SpanContextExtractorType is null || ExtractMethod is null || SetParent is null || StartActiveWithContextMethod is null || carrier == null || SpanCreationSettingsType is null)
            {
                return new NoOpDisposable();
            }

            var scopeExtractor = Activator.CreateInstance(SpanContextExtractorType);
            var genericMethod = ExtractMethod.MakeGenericMethod(carrier.GetType());
            var parentScope = genericMethod.Invoke(scopeExtractor, new object[] { carrier, getter });

            var spanCreationSettings = Activator.CreateInstance(SpanCreationSettingsType);
            SetParent.Invoke(spanCreationSettings, new object[] { parentScope });

            var tracer = GetTracerInstance.Invoke(null, Array.Empty<object>());
            return (IDisposable) StartActiveWithContextMethod.Invoke(tracer, new object[] { operationName, spanCreationSettings });
        }

        public static void ExtractScope<TCarrier>(TCarrier carrier, Func<TCarrier, string, IEnumerable<string>> getter, out ulong traceId, out ulong spanId)
        {
            if (ExtractMethod is null || TraceIdProperty is null || SpanIdProperty is null  || SpanContextExtractorType is null  || carrier == null)
            {
                traceId = 0;
                spanId = 0;
                return;
            }

            var scopeExtractor = Activator.CreateInstance(SpanContextExtractorType);
            var genericMethod = ExtractMethod.MakeGenericMethod(carrier.GetType());
            var parentScope = genericMethod.Invoke(scopeExtractor, new object[] { carrier, getter });
            traceId = (ulong) TraceIdProperty.Invoke(parentScope, null);
            spanId = (ulong) SpanIdProperty.Invoke(parentScope, null);
        }

        public static ulong GetTraceId(IDisposable scope)
        {
            var span = SpanProperty.Invoke(scope, Array.Empty<object>());
            var context = SpanContextProperty.Invoke(span, Array.Empty<object>());
            return (ulong) TraceIdProperty.Invoke(context, Array.Empty<object>());
        }

        public static ulong GetSpanId(IDisposable scope)
        {
            var span = SpanProperty.Invoke(scope, Array.Empty<object>());
            var context = SpanContextProperty.Invoke(span, Array.Empty<object>());
            return (ulong) SpanIdProperty.Invoke(context, Array.Empty<object>());
        }

        public static Task ForceTracerFlushAsync()
        {
            if (GetTracerInstance is null || ForceFlushAsyncMethod is null)
            {
                return Task.CompletedTask;
            }

            var tracer = GetTracerInstance.Invoke(null, Array.Empty<object>());
            return (Task)ForceFlushAsyncMethod.Invoke(tracer, Array.Empty<object>());
        }

        public static IDisposable GetActiveScope()
        {
            if (GetTracerInstance is null || ActiveScopeProperty is null)
            {
                return new NoOpDisposable();
            }

            var tracer = GetTracerInstance.Invoke(null, Array.Empty<object>());
            return (IDisposable) ActiveScopeProperty.Invoke(tracer, Array.Empty<object>());
        }

        public static ulong GetCorrelationIdentifierTraceId()
        {
            if (CorrelationIdentifierTraceIdProperty is null)
            {
                return 0;
            }

            return (ulong)CorrelationIdentifierTraceIdProperty.Invoke(null, Array.Empty<object>());
        }

        public static void TrySetResourceName(object scope, string resourceName)
        {
            if (SpanProperty != null && SetResourceNameProperty != null)
            {
                var span = SpanProperty.Invoke(scope, Array.Empty<object>());
                SetResourceNameProperty.Invoke(span, new object[] { resourceName });
            }
        }

        public static void TrySetTag(object scope, string key, string value)
        {
            if (SpanProperty != null && SetTagMethod != null)
            {
                var span = SpanProperty.Invoke(scope, Array.Empty<object>());
                SetTagMethod.Invoke(span, new object[] { key, value });
            }
        }

        public static ConcurrentDictionary<string, double> GetMetrics(object scope)
        {
            if (SpanProperty != null && SetTagMethod != null)
            {
                var span = SpanProperty.Invoke(scope, Array.Empty<object>());
                foreach (var property in span.GetType()
                    .GetProperties(
                            BindingFlags.Instance |
                            BindingFlags.NonPublic))
                {
                    if (property.Name == "Metrics")
                    {
                        return (ConcurrentDictionary<string, double>) property.GetValue(span);
                    }
                }
            }
            return null;
        }

        public static void TrySetExceptionOnActiveScope(Exception exception)
        {
            if (GetTracerInstance is null || ActiveScopeProperty is null || SpanProperty is null || SetExceptionMethod is null)
            {
                return;
            }

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

        class NoOpDisposable : IDisposable
        {
            public void Dispose() { }
        }
    }
}
