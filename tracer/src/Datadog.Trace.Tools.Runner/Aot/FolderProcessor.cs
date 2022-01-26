// <copyright file="FolderProcessor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Datadog.Trace.ClrProfiler;
using dnlib.DotNet;
using Spectre.Console;

namespace Datadog.Trace.Tools.Runner.Aot
{
    internal class FolderProcessor
    {
        private static readonly NativeCallTargetDefinition[] Definitions;
        private static readonly NativeCallTargetDefinition[] DerivedDefinitions;

        static FolderProcessor()
        {
            Definitions = InstrumentationDefinitions.GetAllDefinitions().Definitions;
            DerivedDefinitions = InstrumentationDefinitions.GetDerivedDefinitions().Definitions;
        }

        public static void Process(string inputFolder, string outputFolder)
        {
            if (!Directory.Exists(inputFolder))
            {
                throw new DirectoryNotFoundException("Input folder doesn't exist.");
            }

            if (!Directory.Exists(outputFolder))
            {
                throw new DirectoryNotFoundException("Output folder doesn't exist.");
            }

            List<AssemblyProcessor> assembliesProcessor = new();
            foreach (var file in Directory.EnumerateFiles(inputFolder, "*.dll", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    var asmName = AssemblyName.GetAssemblyName(file);

                    var definitions = Definitions.Where(def =>
                    {
                        if (def.TargetAssembly != asmName.Name)
                        {
                            return false;
                        }

                        if (asmName.Version is not null)
                        {
                            var minVersion = new Version(def.TargetMinimumMajor, def.TargetMinimumMinor, def.TargetMinimumPatch);
                            var maxVersion = new Version(def.TargetMaximumMajor, def.TargetMaximumMinor, def.TargetMaximumPatch);

                            if (asmName.Version < minVersion)
                            {
                                return false;
                            }

                            if (asmName.Version > maxVersion)
                            {
                                return false;
                            }
                        }

                        return true;
                    }).ToArray();

                    if (definitions.Length > 0)
                    {
                        assembliesProcessor.Add(new AssemblyProcessor(file, Path.Combine(outputFolder, Path.GetFileName(file)), definitions, DerivedDefinitions));
                        AnsiConsole.WriteLine($"{asmName.FullName} => {definitions.Length}");
                    }
                    else
                    {
                        ModuleContext modCtx = ModuleDef.CreateModuleContext();
                        using (ModuleDefMD module = ModuleDefMD.Load(file, modCtx))
                        {
                            bool added = false;
                            var asmRefs = module.GetAssemblyRefs();
                            foreach (var asmRef in asmRefs)
                            {
                                foreach (var def in DerivedDefinitions)
                                {
                                    if (def.TargetAssembly == asmRef.Name.String)
                                    {
                                        var minVersion = new Version(def.TargetMinimumMajor, def.TargetMinimumMinor, def.TargetMinimumPatch);
                                        var maxVersion = new Version(def.TargetMaximumMajor, def.TargetMaximumMinor, def.TargetMaximumPatch);

                                        if (asmRef.Version >= minVersion && asmRef.Version <= maxVersion)
                                        {
                                            assembliesProcessor.Add(new AssemblyProcessor(file, Path.Combine(outputFolder, Path.GetFileName(file)), null, DerivedDefinitions));
                                            AnsiConsole.WriteLine($"{asmName.FullName} => {asmRef.Name.String}");
                                            added = true;
                                            break;
                                        }
                                    }
                                }

                                if (added)
                                {
                                    break;
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // .
                }
            }

            // Call process
        }
    }
}
