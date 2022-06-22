using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Datadog.InstrumentedAssemblyGenerator
{
    internal enum InstrumentedAssemblyGenerationResult
    {
        /// <summary>
        /// If everything failed
        /// </summary>
        Failed,
        /// <summary>
        /// If we succeeded to generate part of the methods in module or part of the modules 
        /// </summary>
        PartiallySucceeded,
        /// <summary>
        /// If everything goes right
        /// </summary>
        Succeeded
    }

    /// <summary>
    /// The engine responsible for instrumentation verification.
    /// </summary>
    public static class InstrumentedAssemblyGeneration
    {
        /// <summary>
        /// Generates assembly on disk which contain the same instrumentation we performed at runtime through the CLR Profiling API.
        /// See /docs/InstrumentationVerification.md
        /// </summary>
        /// <param name="assemblyGeneratorArgs">The arguments for the generation process</param>
        /// <returns>The list of instrumented assemblies that were generated, and the list of methods that were instrumented in each.</returns>
        public static List<(string modulePath, List<string> methods)> Generate(AssemblyGeneratorArgs assemblyGeneratorArgs)
        {
            ValidateInputFolders(assemblyGeneratorArgs);
            CreateOutputFolders(assemblyGeneratorArgs);

            var assemblyGenerator = new InstrumentedAssemblyGenerator(assemblyGeneratorArgs);

            assemblyGenerator.Initialize();

            var result = assemblyGenerator.ModifyMethods();

            if (result == InstrumentedAssemblyGenerationResult.Failed)
            {
                throw new Exception("Failed to generate instrumented assembly");
            }

            var originalModules = File.ReadLines(assemblyGeneratorArgs.OriginalModulesFilePath).ToList();
            CopyReferencesToExportDirectory(assemblyGeneratorArgs.InstrumentedAssembliesFolder,
                                            originalModules,
                                            assemblyGeneratorArgs.ModulesToGenerate);

            return assemblyGenerator.ExportedModulesPathAndMethods;
        }

        private static void ValidateInputFolders(AssemblyGeneratorArgs args)
        {
            if (!Directory.Exists(args.InstrumentationInputLogs))
            {
                throw new ArgumentException($"Directory not exist {args.InstrumentationInputLogs}");
            }

            if (!Directory.EnumerateFiles(args.InstrumentationInputLogs, $"*{InstrumentedAssemblyGeneratorConsts.InstrumentedLogFileExtension}").Any())
            {
                throw new ArgumentException($"No {InstrumentedAssemblyGeneratorConsts.InstrumentedLogFileExtension} files were found in {args.InstrumentationInputLogs}");
            }

            if (!Directory.EnumerateFiles(args.InstrumentationInputLogs, $"*{InstrumentedAssemblyGeneratorConsts.ModuleMembersFileExtension}").Any())
            {
                throw new ArgumentException($"No {InstrumentedAssemblyGeneratorConsts.ModuleMembersFileExtension} files were found in {args.InstrumentationInputLogs}");
            }
        }

        private static void CreateOutputFolders(AssemblyGeneratorArgs args)
        {
            CreateFolderOrClearIfAlreadyExists(args.InstrumentedAssembliesFolder);
            CreateFolderOrClearIfAlreadyExists(args.InstrumentedMethodsFolder);
        }

        private static void CreateFolderOrClearIfAlreadyExists(string folderPath)
        {
            if (Directory.Exists(folderPath))
            {
                new DirectoryInfo(folderPath).ClearDirectory();
            }

            Directory.CreateDirectory(folderPath);
        }

        private static void CopyReferencesToExportDirectory(string exportDirectory, List<string> modules, string[] skip)
        {
            foreach (string module in modules.Distinct())
            {
                try
                {
                    string ext = Path.GetExtension(module).ToLower();
                    if (ext != ".dll" && ext != ".exe")
                    {
                        continue;
                    }

                    if (skip.Any(m => m.Equals(new FileInfo(module).Name, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }
                    string copyTo = Path.Combine(exportDirectory, Path.GetFileName(module));
                    if (File.Exists(copyTo))
                    {
                        continue;
                    }

                    File.Copy(module, copyTo);
                }
                catch (Exception e)
                {
                    Logger.Debug(e.Message);
                }
            }
        }
    }
}
