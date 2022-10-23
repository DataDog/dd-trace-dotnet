using System;
using System.IO;
using static Datadog.InstrumentedAssemblyGenerator.InstrumentedAssemblyGeneratorConsts;
#pragma warning disable CS1591

namespace Datadog.InstrumentedAssemblyGenerator
{
    public class AssemblyGeneratorArgs
    {
        public AssemblyGeneratorArgs(string instrumentationLogsBaseFolder, bool copyOriginalModulesToDisk = false, string[] modulesToVerify = null, string[] methodsToVerify = null)
        {
            InstrumentationLogsBaseFolder = instrumentationLogsBaseFolder;
            ModulesToGenerate = modulesToVerify ?? Array.Empty<string>();
            MethodsToVerify = methodsToVerify ?? Array.Empty<string>();
            if (copyOriginalModulesToDisk)
            {
                OriginalModulesFolder = Path.Combine(InstrumentationLogsBaseFolder, InstrumentedAssembliesFolderName);
            }
        }

        public string InstrumentationLogsBaseFolder { get; }

        public string[] ModulesToGenerate { get; }

        public string[] MethodsToVerify { get; }

        public string InstrumentationInputLogs => Path.Combine(InstrumentationLogsBaseFolder, InstrumentedAssemblyGeneratorInputFolder);

        public string OriginalModulesFilePath => Path.Combine(InstrumentationInputLogs, ModulesFileName);

        public string OriginalModulesFolder { get; }

        internal string InstrumentedAssembliesFolder => Path.Combine(InstrumentationLogsBaseFolder, InstrumentedAssembliesFolderName);

        internal string InstrumentedMethodsFolder => Path.Combine(InstrumentedAssembliesFolder, InstrumentedMethodsInstructionsFolderName);
    }
}
