using System;
using System.IO;
using static Datadog.InstrumentedAssemblyGenerator.InstrumentedAssemblyGeneratorConsts;

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

        public string InstrumentedAssembliesFolder => Path.Combine(InstrumentationLogsBaseFolder, InstrumentedAssembliesFolderName);

        public string InstrumentedMethodsFolder => Path.Combine(InstrumentedAssembliesFolder, InstrumentedMethodsInstructionsFolderName);
    }
}