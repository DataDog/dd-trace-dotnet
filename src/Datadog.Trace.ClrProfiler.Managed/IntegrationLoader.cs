using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.Attributes;
using Mono.Cecil;

namespace Datadog.Trace.ClrProfiler
{
    internal static class IntegrationLoader
    {
        private static Dictionary<string, List<Type>> tracersByAssemblyName = new Dictionary<string, List<Type>>();

        static IntegrationLoader()
        {
            var types = typeof(IntegrationLoader).Assembly.GetTypes();
            foreach (var type in types)
            {
                foreach (var attr in System.Attribute.GetCustomAttributes(type))
                {
                    if (attr is TraceTypeAttribute traceTypeAttribute)
                    {
                        if (!tracersByAssemblyName.TryGetValue(traceTypeAttribute.AssemblyName, out var tracers))
                        {
                            tracers = new List<Type>();
                            tracersByAssemblyName[traceTypeAttribute.AssemblyName] = tracers;
                        }

                        tracers.Add(type);
                    }
                }
            }
        }

        public static void ProcessAssembly(Assembly assembly)
        {
            var assemblyName = assembly.GetName().Name;

            if (!tracersByAssemblyName.TryGetValue(assemblyName, out var tracers))
            {
                return;
            }

            foreach (var type in tracers)
            {
                ProcessTracer(assembly, type);
            }
        }

        private static void ProcessTracer(Assembly assembly, Type tracer)
        {
            var assemblyName = assembly.GetName().Name;

            foreach (var attr in System.Attribute.GetCustomAttributes(tracer))
            {
                if (attr is TraceTypeAttribute traceTypeAttribute && traceTypeAttribute.AssemblyName == assemblyName)
                {
                    var target = assembly.GetType(traceTypeAttribute.TypeName);
                    if (target == null)
                    {
                        continue;
                    }

                    var onEntersBySignature = GetMethodEnterTracers(tracer);

                    var methods = target.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
                    foreach (var method in methods)
                    {
                        var methodName = method.ToString();
                        if (!onEntersBySignature.TryGetValue(methodName, out var onEnters))
                        {
                            onEnters = new List<MethodInfo>();
                        }

                        if (onEnters.Count > 0)
                        {
                            AssemblyDefinition.ReadAssembly()
                            ModuleDefinition.ReadModule()
                        }
                    }
                }
            }
        }

        private static Dictionary<string, List<MethodInfo>> GetMethodEnterTracers(Type tracer)
        {
            var bySignature = new Dictionary<string, List<MethodInfo>>();
            foreach (var mi in tracer.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static))
            {
                foreach (var attr in System.Attribute.GetCustomAttributes(mi))
                {
                    if (attr is TraceMethodEnterAttribute traceMethodEnter)
                    {
                        if (!bySignature.TryGetValue(traceMethodEnter.Signature, out var methods))
                        {
                            methods = new List<MethodInfo>();
                            bySignature[traceMethodEnter.Signature] = methods;
                        }

                        methods.Add(mi);
                    }
                }
            }

            return bySignature;
        }

        private static Func<T1, TOutput> MakeFunc<T1, TOutput>(Func<object[], object> onEnter, Func<T1, TOutput> original)
        {
            return t1 =>
            {
                onEnter(new object[] { t1 });
                return original(t1);
            };
        }
    }
}
