using System;
using System.IO;
using static Datadog.InstrumentedAssemblyGenerator.InstrumentedAssemblyGeneratorConsts;
#pragma warning disable CS1591

namespace Datadog.InstrumentedAssemblyGenerator
{
    public class AssemblyGeneratorArgs
    {
        public AssemblyGeneratorArgs(string instrumentationLogsBaseFolder, string[] modulesToVerify = null)
        {
            InstrumentationLogsBaseFolder = instrumentationLogsBaseFolder;
            ModulesToGenerate = modulesToVerify ?? Array.Empty<string>();
        }

        public string InstrumentationLogsBaseFolder { get; }

        public string[] ModulesToGenerate { get; }

        public string InstrumentationInputLogs => Path.Combine(InstrumentationLogsBaseFolder, InstrumentedAssemblyGeneratorInputFolder);
        
        public string OriginalModulesFilePath => Path.Combine(InstrumentationInputLogs, ModulesFileName);

        public string OriginalModulesFolder => Path.Combine(InstrumentationLogsBaseFolder, OriginalModulesFolderName);

        internal string InstrumentedAssembliesFolder => Path.Combine(InstrumentationLogsBaseFolder, InstrumentedAssembliesFolderName);

        internal string InstrumentedMethodsFolder => Path.Combine(InstrumentedAssembliesFolder, InstrumentedMethodsInstructionsFolderName);
    }
}
