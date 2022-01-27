// <copyright file="AotProcessor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler;
using dnlib.DotNet;
using Spectre.Console;

namespace Datadog.Trace.Tools.Runner.Aot
{
    internal class AotProcessor
    {
        private static readonly NativeCallTargetDefinition[] Definitions;
        private static readonly NativeCallTargetDefinition[] DerivedDefinitions;

        static AotProcessor()
        {
            Definitions = InstrumentationDefinitions.GetAllDefinitions().Definitions;
            DerivedDefinitions = InstrumentationDefinitions.GetDerivedDefinitions().Definitions;
        }

        public static void ProcessFolder(string inputFolder, string outputFolder)
        {
            if (!Directory.Exists(inputFolder))
            {
                throw new DirectoryNotFoundException("Input folder doesn't exist.");
            }

            if (!Directory.Exists(outputFolder))
            {
                throw new DirectoryNotFoundException("Output folder doesn't exist.");
            }

            int processed = 0;
            Parallel.ForEach(Directory.EnumerateFiles(inputFolder, "*.dll", SearchOption.TopDirectoryOnly), file =>
            {
                if (TryProcessAssembly(file, Path.Combine(outputFolder, Path.GetFileName(file))))
                {
                    Interlocked.Increment(ref processed);
                }
            });

            AnsiConsole.WriteLine($"{processed} files processed.");
        }

        private static bool TryProcessAssembly(string inputPath, string outputPath)
        {
            try
            {
                ModuleContext modCtx = ModuleDef.CreateModuleContext();
                using (ModuleDefMD module = ModuleDefMD.Load(inputPath, modCtx))
                {
                    var lstDefinitions = new List<NativeCallTargetDefinition>();
                    var lstDerived = new List<NativeCallTargetDefinition>();

                    var assemblyDef = module.Assembly;

                    // Extract direct definitions
                    foreach (var definition in Definitions)
                    {
                        if (definition.TargetAssembly != assemblyDef.Name)
                        {
                            continue;
                        }

                        if (assemblyDef.Version is not null)
                        {
                            var minVersion = new Version(definition.TargetMinimumMajor, definition.TargetMinimumMinor, definition.TargetMinimumPatch);
                            var maxVersion = new Version(definition.TargetMaximumMajor, definition.TargetMaximumMinor, definition.TargetMaximumPatch);

                            if (assemblyDef.Version < minVersion)
                            {
                                continue;
                            }

                            if (assemblyDef.Version > maxVersion)
                            {
                                continue;
                            }
                        }

                        var typeDef = module.ExportedTypes.FirstOrDefault(type => type.FullName == definition.TargetType)?.Resolve();

                        lstDefinitions.Add(definition);
                    }

                    // Extract derived definitions
                    var assemblyRefs = module.GetAssemblyRefs();
                    foreach (var assemblyRef in assemblyRefs)
                    {
                        foreach (var definition in DerivedDefinitions)
                        {
                            if (definition.TargetAssembly != assemblyRef.Name.String)
                            {
                                continue;
                            }

                            var minVersion = new Version(definition.TargetMinimumMajor, definition.TargetMinimumMinor, definition.TargetMinimumPatch);
                            var maxVersion = new Version(definition.TargetMaximumMajor, definition.TargetMaximumMinor, definition.TargetMaximumPatch);

                            if (assemblyDef.Version < minVersion)
                            {
                                continue;
                            }

                            if (assemblyDef.Version > maxVersion)
                            {
                                continue;
                            }

                            lstDerived.Add(definition);
                        }
                    }

                    if (lstDefinitions.Count == 0 && lstDerived.Count == 0)
                    {
                        return false;
                    }

                    AnsiConsole.WriteLine($"{assemblyDef.FullName} => {lstDefinitions.Count} - {lstDerived.Count}");
                }
            }
            catch (BadImageFormatException)
            {
                return false;
            }
            catch (Exception ex)
            {
                Utils.WriteError(ex.ToString());
                return false;
            }

            return true;
        }
    }
}
