#pragma once

namespace instrumented_assembly_generator
{
const shared::WSTRING FileNameSeparator = WStr("@");
const shared::WSTRING BinaryFilePrefix = WStr("bin@");
const shared::WSTRING TextFilePrefix = WStr("txt@");
const shared::WSTRING ModuleMembersFileExtension = WStr(".modulemembers");
const shared::WSTRING InstrumentedLogFileExtension = WStr(".instrlog");
const shared::WSTRING ModulesFileName = WStr("ModulesLoaded.modules");
const shared::WSTRING InstrumentedAssemblyGeneratorLogsFolder = WStr("InstrumentationVerification");
const shared::WSTRING InstrumentedAssemblyGeneratorInputFolder = WStr("INPUT_InstrumentationLogs");
const shared::WSTRING OriginalModulesFolder = WStr("INPUT_OriginalAssemblies");
} // namespace instrumented_assembly_generator