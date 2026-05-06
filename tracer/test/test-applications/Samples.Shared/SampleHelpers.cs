#nullable enable

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
        private static readonly Type? InstrumentationType = Type.GetType("Datadog.Trace.ClrProfiler.Instrumentation, Datadog.Trace");
        private static readonly Type? NativeMethodsType = Type.GetType("Datadog.Trace.ClrProfiler.NativeMethods, Datadog.Trace");
        private static readonly Type? TracerType = Type.GetType("Datadog.Trace.Tracer, Datadog.Trace");
        private static readonly Type? ScopeType = Type.GetType("Datadog.Trace.Scope, Datadog.Trace");
        private static readonly Type? SpanType = Type.GetType("Datadog.Trace.Span, Datadog.Trace");
        private static readonly Type? SpanContextExtractorType = Type.GetType("Datadog.Trace.SpanContextExtractor, Datadog.Trace");
        private static readonly Type? SpanContextInjectorType = Type.GetType("Datadog.Trace.SpanContextInjector, Datadog.Trace");
        private static readonly Type? SpanCreationSettingsType = Type.GetType("Datadog.Trace.SpanCreationSettings, Datadog.Trace");
        private static readonly Type? SpanContextType = Type.GetType("Datadog.Trace.SpanContext, Datadog.Trace");
        private static readonly Type? TracerSettingsType = Type.GetType("Datadog.Trace.Configuration.TracerSettings, Datadog.Trace");
        private static readonly Type? TracerConstantsType = Type.GetType("Datadog.Trace.TracerConstants, Datadog.Trace");
        private static readonly Type? ProcessHelpersType = Type.GetType("Datadog.Trace.Util.ProcessHelpers, Datadog.Trace");
        private static readonly Type? UserDetailsType = Type.GetType("Datadog.Trace.UserDetails, Datadog.Trace");
        private static readonly Type? EventTrackingSdk = Type.GetType("Datadog.Trace.AppSec.EventTrackingSdk, Datadog.Trace");
        private static readonly Type? SpanExtensionsType = Type.GetType("Datadog.Trace.SpanExtensions, Datadog.Trace");
        public static readonly Type? IpcClientType = Type.GetType("Datadog.Trace.Ci.Ipc.IpcClient, Datadog.Trace");
        private static readonly PropertyInfo? GetTracerManagerProperty = TracerType?.GetProperty("TracerManager", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly MethodInfo? GetNativeTracerVersionMethod = InstrumentationType?.GetMethod("GetNativeTracerVersion");
        private static readonly MethodInfo? GetTracerInstance = TracerType?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)?.GetMethod;
        private static readonly MethodInfo? StartActiveMethod = TracerType?.GetMethod("StartActive", types: new[] { typeof(string) });
        private static readonly MethodInfo? StartActiveWithContextMethod;
        private static readonly MethodInfo? ExtractMethod = SpanContextExtractorType?.GetMethod("Extract", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        private static readonly MethodInfo? InjectMethod = SpanContextInjectorType?.GetMethod("Inject", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        private static readonly MethodInfo? SetParent = SpanCreationSettingsType?.GetProperty("Parent")?.SetMethod;
        private static readonly MethodInfo? ForceFlushAsyncMethod = TracerType?.GetMethod("ForceFlushAsync", BindingFlags.Public | BindingFlags.Instance);
        private static readonly MethodInfo? ActiveScopeProperty = TracerType?.GetProperty("ActiveScope")?.GetMethod;
        private static readonly MethodInfo? ConfigureMethod = TracerType?.GetMethod("Configure");
        private static readonly MethodInfo? TraceIdProperty = SpanContextType?.GetProperty("TraceId")?.GetMethod;
        private static readonly MethodInfo? SpanIdProperty = SpanContextType?.GetProperty("SpanId")?.GetMethod;
        private static readonly MethodInfo? GetOrMakeSamplingDecisionMethod = SpanContextType?.GetMethod("GetOrMakeSamplingDecision", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly MethodInfo? SpanProperty = ScopeType?.GetProperty("Span", BindingFlags.NonPublic | BindingFlags.Instance)?.GetMethod;
        private static readonly MethodInfo? SpanContextProperty = SpanType?.GetProperty("Context", BindingFlags.NonPublic | BindingFlags.Instance)?.GetMethod;
        private static readonly MethodInfo? SetServiceNameProperty = SpanType?.GetProperty("ServiceName", BindingFlags.NonPublic | BindingFlags.Instance)?.SetMethod;
        private static readonly MethodInfo? SetResourceNameProperty = SpanType?.GetProperty("ResourceName", BindingFlags.NonPublic | BindingFlags.Instance)?.SetMethod;
        private static readonly MethodInfo? SetTagMethod = SpanType?.GetMethod("SetTag", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly MethodInfo? SetExceptionMethod = SpanType?.GetMethod("SetException", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly MethodInfo? FromDefaultSourcesMethod = TracerSettingsType?.GetMethod("FromDefaultSources", BindingFlags.Public | BindingFlags.Static);
        private static readonly MethodInfo? SetServiceName = TracerSettingsType?.GetProperty("ServiceName")?.SetMethod;
        private static readonly MethodInfo? GetMetricMethod = SpanType?.GetMethod("GetMetric", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly MethodInfo? RunCommandMethod = ProcessHelpersType?.GetMethod("TestingOnly_RunCommand", BindingFlags.NonPublic | BindingFlags.Static);
        private static readonly MethodInfo? SetUserIdMethod = UserDetailsType?.GetProperty("Id", BindingFlags.Public | BindingFlags.Instance)?.SetMethod;
        private static readonly MethodInfo? TrackUserLoginSuccessEventMethod;
        private static readonly MethodInfo? TrackUserLoginFailureEventMethod;
#if NETCOREAPP
        private static readonly MethodInfo? SetUserMethod = SpanExtensionsType?.GetMethod("SetUser", BindingFlags.Public | BindingFlags.Static | BindingFlags.DoNotWrapExceptions);
#else
        private static readonly MethodInfo? SetUserMethod = SpanExtensionsType?.GetMethod("SetUser", BindingFlags.Public | BindingFlags.Static);
#endif
        private static readonly FieldInfo? TracerThreePartVersionField = TracerConstantsType?.GetField("ThreePartVersion");

        static SampleHelpers()
        {
            if (TracerType is null)
            {
                Console.WriteLine("*** [Warning] SampleHelpers.TracerType is null so you may experience missing spans. Ensure automatic instrumentation is correctly enabled for this application to make sure spans are generated. ***");
                return;
            }

            if (SpanCreationSettingsType is not null)
            {
                StartActiveWithContextMethod = TracerType.GetMethod("StartActive", types: new[] { typeof(string), SpanCreationSettingsType });
            }

            // Look up the EventTrackingSdk methods here (rather than as field initializers) so the Type[] signature
            // can include TracerType without a null-forgiving cast — the early return above guarantees it's non-null.
            if (EventTrackingSdk is not null)
            {
                TrackUserLoginSuccessEventMethod = EventTrackingSdk.GetMethod(
                    "TrackUserLoginSuccessEvent",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new Type[] { typeof(string), typeof(IDictionary<string, string>), TracerType },
                    null);
                TrackUserLoginFailureEventMethod = EventTrackingSdk.GetMethod(
                    "TrackUserLoginFailureEvent",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new Type[] { typeof(string), typeof(bool), typeof(IDictionary<string, string>), TracerType },
                    null);
            }
        }

        public static void ConfigureTracer(string serviceName)
        {
            if (TracerSettingsType is null || GetTracerInstance is null || ConfigureMethod is null || FromDefaultSourcesMethod is null || SetServiceName is null)
            {
                return;
            }

            var tracerSettings = FromDefaultSourcesMethod.Invoke(null, Array.Empty<object>());
            SetServiceName.Invoke(tracerSettings, new object[] { serviceName });

            var tracer = GetTracerInstance.Invoke(null, Array.Empty<object>());
            ConfigureMethod.Invoke(tracer, new object?[] { tracerSettings });
        }

        public static bool IsProfilerAttached()
        {
            if (NativeMethodsType is null)
            {
                return false;
            }

            try
            {
                var profilerAttachedMethodInfo = NativeMethodsType.GetMethod("IsProfilerAttached");
                return profilerAttachedMethodInfo?.Invoke(null, null) is bool attached && attached;
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
                return (string?)GetNativeTracerVersionMethod?.Invoke(null, Array.Empty<object>()) ?? "None";
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
                return (string?)TracerThreePartVersionField?.GetValue(null) ?? "None";
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
                        unloadModuleMethod?.Invoke(null, new object[] { caller, EventArgs.Empty });
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
            return (IDisposable?)StartActiveMethod.Invoke(tracer, new object[] { operationName }) ?? new NoOpDisposable();
        }

        public static IDisposable CreateScopeWithPropagation<TCarrier>(string operationName, TCarrier carrier, Func<TCarrier, string, IEnumerable<string>> getter)
        {
            if (GetTracerInstance is null || SpanContextExtractorType is null || ExtractMethod is null || SetParent is null || StartActiveWithContextMethod is null || carrier == null || SpanCreationSettingsType is null)
            {
                return new NoOpDisposable();
            }

            var tracer = GetTracerInstance.Invoke(null, Array.Empty<object>());
            string? messageType = null;
            string? source = null;
            var genericMethod = ExtractMethod.MakeGenericMethod(carrier.GetType());
            var parentScope = genericMethod.Invoke(null, new object?[] { tracer, carrier, getter, messageType, source });

            var spanCreationSettings = Activator.CreateInstance(SpanCreationSettingsType);
            SetParent.Invoke(spanCreationSettings, new object?[] { parentScope });

            return (IDisposable?)StartActiveWithContextMethod.Invoke(tracer, new object?[] { operationName, spanCreationSettings }) ?? new NoOpDisposable();
        }

        public static void ExtractScope<TCarrier>(TCarrier carrier, Func<TCarrier, string, IEnumerable<string>> getter, out ulong traceId, out ulong spanId)
        {
            traceId = 0;
            spanId = 0;
            if (ExtractMethod is null || TraceIdProperty is null || SpanIdProperty is null || SpanContextExtractorType is null || carrier == null || GetTracerInstance is null)
            {
                return;
            }

            var tracer = GetTracerInstance.Invoke(null, Array.Empty<object>());
            string? messageType = null;
            string? source = null;
            var genericMethod = ExtractMethod.MakeGenericMethod(carrier.GetType());
            var parentScope = genericMethod.Invoke(null, new object?[] { tracer, carrier, getter, messageType, source });
            traceId = (ulong)(TraceIdProperty.Invoke(parentScope, null) ?? 0UL);
            spanId = (ulong)(SpanIdProperty.Invoke(parentScope, null) ?? 0UL);
        }

        public static void InjectScope<TCarrier>(TCarrier carrier, Action<TCarrier, string, string> setter, object? scope)
        {
            if (InjectMethod is null || SpanContextInjectorType is null || carrier == null || scope is null || GetTracerInstance is null)
            {
                return;
            }

            var tracer = GetTracerInstance.Invoke(null, Array.Empty<object>());
            var genericMethod = InjectMethod.MakeGenericMethod(carrier.GetType());
            string? messageType = null;
            string? target = null;
            genericMethod.Invoke(null, new object?[] { tracer, carrier, setter, scope, messageType, target });
        }

        public static ulong GetTraceId(IDisposable? scope = null)
        {
            var target = scope ?? GetActiveSpanContext();
            if (TraceIdProperty is null || target is null)
            {
                return 0;
            }

            return (ulong)(TraceIdProperty.Invoke(target, Array.Empty<object>()) ?? 0UL);
        }

        public static ulong GetSpanId(IDisposable? scope = null)
        {
            var target = scope ?? GetActiveSpanContext();
            if (SpanIdProperty is null || target is null)
            {
                return 0;
            }

            return (ulong)(SpanIdProperty.Invoke(target, Array.Empty<object>()) ?? 0UL);
        }

        public static int? GetOrMakeSamplingDecision()
        {
            var ctx = GetActiveSpanContext();
            if (GetOrMakeSamplingDecisionMethod is null || ctx is null)
            {
                return null;
            }

            return (int?)GetOrMakeSamplingDecisionMethod.Invoke(ctx, Array.Empty<object>());
        }

        public static Task ForceTracerFlushAsync()
        {
            if (GetTracerInstance is null || ForceFlushAsyncMethod is null)
            {
                return Task.CompletedTask;
            }

            var tracer = GetTracerInstance.Invoke(null, Array.Empty<object>());
            return (Task?)ForceFlushAsyncMethod.Invoke(tracer, Array.Empty<object>()) ?? Task.CompletedTask;
        }

        public static IDisposable GetActiveScope()
        {
            if (GetTracerInstance is null || ActiveScopeProperty is null)
            {
                return new NoOpDisposable();
            }

            var tracer = GetTracerInstance.Invoke(null, Array.Empty<object>());
            return (IDisposable?)ActiveScopeProperty.Invoke(tracer, Array.Empty<object>()) ?? new NoOpDisposable();
        }

        public static object? GetActiveSpanContext()
        {
            if (SpanContextProperty is null || SpanProperty is null)
            {
                return null;
            }

            var span = SpanProperty.Invoke(GetActiveScope(), Array.Empty<object>());
            if (span is null)
            {
                return null;
            }

            return SpanContextProperty.Invoke(span, Array.Empty<object>());
        }

        public static void TrySetResourceName(object scope, string resourceName)
        {
            if (SpanProperty is null || SetResourceNameProperty is null)
            {
                return;
            }

            var span = SpanProperty.Invoke(scope, Array.Empty<object>());
            if (span is null)
            {
                return;
            }

            SetResourceNameProperty.Invoke(span, new object[] { resourceName });
        }

        public static void TrySetServiceName(object scope, string serviceName)
        {
            if (SpanProperty is null || SetServiceNameProperty is null)
            {
                return;
            }

            var span = SpanProperty.Invoke(scope, Array.Empty<object>());
            if (span is null)
            {
                return;
            }

            SetServiceNameProperty.Invoke(span, new object[] { serviceName });
        }

        public static void TrySetTag(object scope, string key, string value)
        {
            if (SpanProperty is null || SetTagMethod is null)
            {
                return;
            }

            var span = SpanProperty.Invoke(scope, Array.Empty<object>());
            if (span is null)
            {
                return;
            }

            SetTagMethod.Invoke(span, new object[] { key, value });
        }

        public static ConcurrentDictionary<string, double>? GetMetrics(object scope)
        {
            if (SpanProperty is null || SetTagMethod is null)
            {
                return null;
            }

            var span = SpanProperty.Invoke(scope, Array.Empty<object>());
            if (span is null)
            {
                return null;
            }

            foreach (var property in span.GetType()
                                         .GetProperties(
                                              BindingFlags.Instance |
                                              BindingFlags.NonPublic))
            {
                if (property.Name == "Metrics")
                {
                    return (ConcurrentDictionary<string, double>?)property.GetValue(span);
                }
            }

            return null;
        }

        public static bool TryGetMetric(object scope, string key, out double metric)
        {
            metric = 0.0;

            if (SpanProperty is null || GetMetricMethod is null)
            {
                return false;
            }

            var span = SpanProperty.Invoke(scope, Array.Empty<object>());
            if (span is null)
            {
                return false;
            }

            var metricValue = GetMetricMethod.Invoke(span, new object[] { key });

            if (metricValue is null)
            {
                return false;
            }

            metric = (double)metricValue;
            return true;
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
            if (span is null)
            {
                return;
            }

            SetExceptionMethod.Invoke(span, new object[] { new Exception() });
        }

        public static List<KeyValuePair<string, string>> GetDatadogEnvironmentVariables()
        {
            var prefixes = new[] { "COR_", "CORECLR_", "DD_", "DATADOG_" };

            var envVars = from envVar in Environment.GetEnvironmentVariables().Cast<DictionaryEntry>()
                          from prefix in prefixes
                          let key = ((envVar.Key as string) ?? string.Empty).ToUpperInvariant() // use ToUpperInvariant() because in .NET Framework string.Contains() doesn't have StringComparison overload
                          let value = (envVar.Value as string) ?? string.Empty
                          where key.StartsWith(prefix, StringComparison.Ordinal) && !key.Contains("API_KEY")
                          orderby key
                          select new KeyValuePair<string, string>(key, value);

            return envVars.ToList();
        }

        public static Task WaitForDiscoveryService()
        {
            if (GetTracerInstance is null)
            {
                return Task.CompletedTask;
            }

            var tracer = GetTracerInstance.Invoke(null, Array.Empty<object>());

            if (tracer == null)
            {
                return Task.CompletedTask;
            }

            var tracerManager = GetTracerManagerProperty?.GetValue(tracer);

            if (tracerManager == null)
            {
                return Task.CompletedTask;
            }

            var discoveryService = tracerManager.GetType()
                                                .GetProperty("DiscoveryService", BindingFlags.Public | BindingFlags.Instance)
                                               ?.GetValue(tracerManager);

            if (discoveryService == null)
            {
                return Task.CompletedTask;
            }

            var result = InstrumentationType?.GetMethod("WaitForDiscoveryService", BindingFlags.NonPublic | BindingFlags.Static)
                                            ?.Invoke(null, new[] { discoveryService });

            return result as Task ?? Task.CompletedTask;
        }

        public static void TrackUserLoginSuccessEvent(string userId, IDictionary<string, string> metadata)
        {
            if (GetTracerInstance is null || TrackUserLoginSuccessEventMethod is null)
            {
                return;
            }

            var tracer = GetTracerInstance.Invoke(null, Array.Empty<object>());
            TrackUserLoginSuccessEventMethod.Invoke(null, new object?[] { userId, metadata, tracer });
        }

        public static void TrackUserLoginFailureEvent(string userId, bool exists, IDictionary<string, string> metadata)
        {
            if (GetTracerInstance is null || TrackUserLoginFailureEventMethod is null)
            {
                return;
            }

            var tracer = GetTracerInstance.Invoke(null, Array.Empty<object>());
            TrackUserLoginFailureEventMethod.Invoke(null, new object?[] { userId, exists, metadata, tracer });
        }

        public static void SetUser(string userId)
        {
            if (SpanProperty is null || SetUserMethod is null || SetUserIdMethod is null || UserDetailsType is null)
            {
                return;
            }

            var scope = GetActiveScope();
            if (scope is null)
            {
                return;
            }

            var span = SpanProperty.Invoke(scope, Array.Empty<object>());
            if (span is null)
            {
                return;
            }

            var userDetails = Activator.CreateInstance(UserDetailsType);
            if (userDetails is null)
            {
                return;
            }

            SetUserIdMethod.Invoke(userDetails, new object[] { userId });
#if NETCOREAPP
            SetUserMethod.Invoke(null, new object[] { span, userDetails });
#else
            try
            {
                // We rely on reflection to invoke Datadog.Trace, Unfortunately, in .NET Framework,
                // the exception is thrown wrapped in a TargetInvocationException, which is not
                // handled by the aspnet middleware pipeline, so we "manuallY" unwrap it
                SetUserMethod.Invoke(null, new object[] { span, userDetails });
            }
            catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException != null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            }
#endif
        }

        public static void RunCommand(string cmd, string? args = null)
        {
            RunCommandMethod?.Invoke(null, new object?[] { cmd, args });
        }

        private class NoOpDisposable : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
