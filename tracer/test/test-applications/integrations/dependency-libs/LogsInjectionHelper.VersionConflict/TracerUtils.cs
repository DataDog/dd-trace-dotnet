using System;
using System.Linq;
using System.Reflection;
using Datadog.Trace;

namespace LogsInjectionHelper.VersionConflict
{
    public class TracerUtils
    {
        private static readonly Version _manualTracingVersion = typeof(Tracer).Assembly.GetName().Version;
        private static Assembly _automaticAssembly;

        private static bool VersionIsLowerThanManualTracingVersion(Version version)
        {
#if NETFRAMEWORK
            return version < _manualTracingVersion;
#else
            return version == _manualTracingVersion;
#endif
        }

        private static bool VersionIsHigherThanManualTracingVersion(Version version)
        {
            return version > _manualTracingVersion;
        }


        public static IDisposable StartAutomaticTraceLowerAssemblyVersion(string operationName)
        {
            return StartAutomaticTrace(operationName, VersionIsLowerThanManualTracingVersion);
        }

        public static IDisposable StartAutomaticTraceHigherAssemblyVersion(string operationName)
        {
            return StartAutomaticTrace(operationName, VersionIsHigherThanManualTracingVersion);
        }

        private static IDisposable StartAutomaticTrace(string operationName, Func<Version, bool> versionComparisonFunc)
        {
            if (_automaticAssembly is null)
            {
                // Get the Datadog.Trace.Tracer type from the automatic instrumentation assembly
#if NETFRAMEWORK
                _automaticAssembly = System.AppDomain.CurrentDomain.GetAssemblies().Single(asm => asm.GetName().Name.Equals("Datadog.Trace") && versionComparisonFunc(asm.GetName().Version));
#elif !NETCOREAPP2_1
                foreach (var alc in System.Runtime.Loader.AssemblyLoadContext.All)
                {
                    _automaticAssembly = alc.Assemblies.SingleOrDefault(asm => asm.GetName().Name.Equals("Datadog.Trace") && versionComparisonFunc(asm.GetName().Version));
                    if (_automaticAssembly != null)
                    {
                        break;
                    }
                }
#endif
            }

            Type tracerType = _automaticAssembly.GetType("Datadog.Trace.Tracer");

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
