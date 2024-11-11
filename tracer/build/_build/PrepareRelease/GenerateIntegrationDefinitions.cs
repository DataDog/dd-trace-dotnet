// <copyright file="GenerateIntegrationDefinitions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Newtonsoft.Json;
using Nuke.Common.IO;
using static CodeGenerators.CallTargetsGenerator;

namespace PrepareRelease
{
    public static class GenerateIntegrationDefinitions
    {
        public static List<InstrumentedAssembly> GetAllIntegrations(ICollection<string> assemblyPaths, AbsolutePath dependabotJsonFile)
        {
            var callTargetIntegrations = Enumerable.Empty<InstrumentedAssembly>();

            foreach (var path in assemblyPaths)
            {
                Console.WriteLine($"Reading integrations for {path}...");
                var assemblyLoadContext = new CustomAssemblyLoadContext(Path.GetDirectoryName(path));
                var assembly = assemblyLoadContext.LoadFromAssemblyPath(path);

                callTargetIntegrations = callTargetIntegrations.Concat(GetCallTargetIntegrations(assembly, dependabotJsonFile));

                assemblyLoadContext.Unload();
            }

            return callTargetIntegrations.ToList();
        }

        static IEnumerable<InstrumentedAssembly> GetCallTargetIntegrations(Assembly assembly, AbsolutePath dependabotJsonFile)
        {
            var definitions = JsonConvert.DeserializeObject<CallTargetDefinitionSource[]>(File.ReadAllText(dependabotJsonFile));
            return definitions
                  .Select(x => new InstrumentedAssembly
                  {
                      IntegrationName = x.IntegrationName,
                      TargetAssembly = x.AssemblyName,
                      TargetMinimumMajor = x.MinimumVersion.Major,
                      TargetMinimumMinor = x.MinimumVersion.Minor,
                      TargetMinimumPatch = x.MinimumVersion.Patch,
                      TargetMaximumMajor = x.MaximumVersion.Major,
                      TargetMaximumMinor = x.MaximumVersion.Minor,
                      TargetMaximumPatch = x.MaximumVersion.Patch,
                  })
                  .Distinct()
                  .ToList();
           
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
