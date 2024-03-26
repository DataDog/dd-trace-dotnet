using System;
using System.IO;
using static Datadog.InstrumentedAssemblyGenerator.InstrumentedAssemblyGeneratorConsts;
#pragma warning disable CS1591

namespace Datadog.InstrumentedAssemblyGenerator
{
    public class AssemblyGeneratorArgs
    {
        public AssemblyGeneratorArgs(string instrumentationLogsBaseFolder, bool copyOriginalModulesToDisk = false, string[] modulesToVerify = null)
        {
            InstrumentationLogsBaseFolder = instrumentationLogsBaseFolder;
            ModulesToGenerate = modulesToVerify ?? Array.Empty<string>();
            if (copyOriginalModulesToDisk)
            {
                // If DD_COPY_ORIGINALS_MODULES_TO_DISK was enabled, this folder will contain all the assemblies that were loaded at runtime,
                // and we'll read the original assemblies from there exclusively.
                // Otherwise, we assume are running in the same machine as the customer application and that files haven't changed,
                // and load the assemblies from their original locations.
                OriginalModulesFolder = Path.Combine(InstrumentationLogsBaseFolder, OriginalModulesFolderName);
            }
        }

        public string InstrumentationLogsBaseFolder { get; }

        public string[] ModulesToGenerate { get; }

        public string InstrumentationInputLogs => Path.Combine(InstrumentationLogsBaseFolder, InstrumentedAssemblyGeneratorInputFolder);

        public string OriginalModulesFilePath => Path.Combine(InstrumentationInputLogs, ModulesFileName);

        public string OriginalModulesFolder { get; }

        internal string InstrumentedAssembliesFolder => Path.Combine(InstrumentationLogsBaseFolder, InstrumentedAssembliesFolderName);

        internal string InstrumentedMethodsFolder => Path.Combine(InstrumentedAssembliesFolder, InstrumentedMethodsInstructionsFolderName);

        internal void PrintArgs()
        {
            Logger.Info($"Instrumentation input logs = {InstrumentationInputLogs}");
            Logger.Info($"Copy original modules = {(string.IsNullOrEmpty(OriginalModulesFolder) ? "false" : "true")}");
            Logger.Info($"ModulesToGenerate = {(ModulesToGenerate.Length == 0 ? "default" : string.Join(";", ModulesToGenerate))}");
        }
    }
}
