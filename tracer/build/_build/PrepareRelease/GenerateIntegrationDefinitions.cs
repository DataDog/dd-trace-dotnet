// <copyright file="GenerateIntegrationDefinitions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;

namespace PrepareRelease
{
    public static class GenerateIntegrationDefinitions
    {
        public static List<InstrumentedAssembly> GetAllIntegrations(ICollection<string> assemblyPaths)
        {
            var callTargetIntegrations = Enumerable.Empty<InstrumentedAssembly>();

            foreach (var path in assemblyPaths)
            {
                Console.WriteLine($"Reading integrations for {path}...");
                var assemblyLoadContext = new CustomAssemblyLoadContext(Path.GetDirectoryName(path));
                var assembly = assemblyLoadContext.LoadFromAssemblyPath(path);

                callTargetIntegrations = callTargetIntegrations.Concat(GetCallTargetIntegrations(assembly));

                assemblyLoadContext.Unload();
            }

            return callTargetIntegrations.ToList();
        }

        static IEnumerable<InstrumentedAssembly> GetCallTargetIntegrations(Assembly assembly)
        {
            var definitionsClass = assembly.GetType("Datadog.Trace.ClrProfiler.InstrumentationDefinitions");
            var definitionsMethod = definitionsClass
               .GetMethod("GetAllDefinitionsNative", BindingFlags.Static | BindingFlags.NonPublic);
            var derivedDefinitionsMethod = definitionsClass
                   .GetMethod("GetAllDerivedDefinitionsNative", BindingFlags.Static | BindingFlags.NonPublic);
            var getIntegrationIdMethod = definitionsClass
                   .GetMethod("GetIntegrationId", BindingFlags.Static | BindingFlags.NonPublic);
            var getAdoNetIntegrationIdMethod = definitionsClass
                   .GetMethod("GetAdoNetIntegrationId", BindingFlags.Static | BindingFlags.Public);

            var integrationIdExtensionsClass = assembly.GetType("Datadog.Trace.Configuration.IntegrationIdExtensions");
            var toStringFastMethod = integrationIdExtensionsClass.GetMethod("ToStringFast", BindingFlags.Static | BindingFlags.Public);

            var structDefinition = assembly.GetType("Datadog.Trace.ClrProfiler.NativeCallTargetDefinition");

            Array definitions = (Array)definitionsMethod.Invoke(null, Array.Empty<object>());
            Array derivedDefinitions = (Array)derivedDefinitionsMethod.Invoke(null, Array.Empty<object>());

            return definitions
                  .Cast<object>()
                  .Concat(derivedDefinitions.Cast<object>())
                  .Select(x => new InstrumentedAssembly
                  {
                      IntegrationName = GetIntegrationName(structDefinition, x, toStringFastMethod, getIntegrationIdMethod, getAdoNetIntegrationIdMethod),
                      TargetAssembly = Marshal.PtrToStringUni((IntPtr) structDefinition.GetField("TargetAssembly").GetValue(x)),
                      TargetMinimumMajor = (ushort) structDefinition.GetField("TargetMinimumMajor").GetValue(x),
                      TargetMinimumMinor = (ushort) structDefinition.GetField("TargetMinimumMinor").GetValue(x),
                      TargetMinimumPatch = (ushort) structDefinition.GetField("TargetMinimumPatch").GetValue(x),
                      TargetMaximumMajor = (ushort) structDefinition.GetField("TargetMaximumMajor").GetValue(x),
                      TargetMaximumMinor = (ushort) structDefinition.GetField("TargetMaximumMinor").GetValue(x),
                      TargetMaximumPatch = (ushort) structDefinition.GetField("TargetMaximumPatch").GetValue(x),
                  })
                  .Distinct()
                  .ToList();
            
            static string GetIntegrationName(Type structDefinition, object definition, MethodInfo toStringFast, MethodInfo getIntegrationId, MethodInfo getAdoNetIntegrationId)
            {
                var targetAssemblyName = Marshal.PtrToStringUni((IntPtr) structDefinition.GetField("TargetAssembly").GetValue(definition));
                var targetTypeName = Marshal.PtrToStringUni((IntPtr) structDefinition.GetField("TargetType").GetValue(definition));
                // var targetType = assembly.GetType(targetTypeName);
                
                var integrationType = Marshal.PtrToStringUni((IntPtr) structDefinition.GetField("IntegrationType").GetValue(definition));
                // can't get the actual types we need, so hack it
                var integrationId = getIntegrationId.Invoke(null, new object[] {integrationType, structDefinition});
                var integrationName = (string) toStringFast.Invoke(null, new[] {integrationId});
                if (integrationName == "AdoNet")
                {
                    // use the other method
                    integrationId = getAdoNetIntegrationId.Invoke(null, new object[] {integrationType, targetTypeName, targetAssemblyName});
                    integrationName = (string) toStringFast.Invoke(null, new[] {integrationId});
                }

                return  integrationName;
            }
        }

        class CustomAssemblyLoadContext : AssemblyLoadContext
        {
            readonly string _assemblyLoadPath;

            public CustomAssemblyLoadContext(string assemblyLoadPath)
                : base("IntegrationsJsonLoadContext", true)
            {
                _assemblyLoadPath = assemblyLoadPath;
            }

            protected override Assembly Load(AssemblyName assemblyName)
            {
                var assemblyPath = Path.Combine(_assemblyLoadPath, $"{assemblyName.Name}.dll");
                if (File.Exists(assemblyPath))
                {
                    return LoadFromAssemblyPath(assemblyPath);
                }

                return null;
            }

        }
    }
}
