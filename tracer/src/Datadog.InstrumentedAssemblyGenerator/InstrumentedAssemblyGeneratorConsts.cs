#pragma warning disable CS1591
namespace Datadog.InstrumentedAssemblyGenerator
{
    public class InstrumentedAssemblyGeneratorConsts
    {
        // !!!!!! Keep compatibility with the consts used in the native loader
        // (\DataDog\dd-trace-dotnet\tracer\src\Datadog.AutoInstrumentation.NativeLoader\instrumented_assembly_generator_consts.h) !!!!!
        public const string InstrumentedAssembliesFolderName = "OUTPUT_InstrumentedAssemblies";
        public const string OriginalModulesFolderName = "INPUT_OriginalAssemblies";
        public const string InstrumentedAssemblyGeneratorInputFolder = "INPUT_InstrumentationLogs";
        public const string InstrumentedAssemblyGeneratorLogsFolder = "InstrumentationVerification";

        internal const string MetadataValueSeparator = "@";
        internal const int InstrumentedLogFileParts = 10;
        internal const int ModuleMembersFileParts = 2;
        internal const string BinaryFilePrefix = "bin@";
        internal const string TextFilePrefix = "txt@";
        internal const string InstrumentedLogFileExtension = ".instrlog";
        internal const string ModuleMembersFileExtension = ".modulemembers";
        internal const string ModulesFileName = "ModulesLoaded.modules";
        internal const string InstrumentedMethodsInstructionsFolderName = "InstrumentedMethodsInstructions";
    }
}
