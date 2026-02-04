// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "DebugInfoStore.h"

#include "COMHelpers.h"
#include "IConfiguration.h"
#include "Log.h"

#include "shared/src/native-src/string.h"

#define GUID_DEFINED
#define MDTOKEN_DEFINED
#include "shared/src/native-lib/PPDB/inc/PPDBReader.hpp"
#undef MDTOKEN_DEFINED
#undef GUID_DEFINED

#ifdef _WINDOWS
#include <atlbase.h>
#include "..\Datadog.Profiler.Native.Windows\DbgHelpParser.h"
#include "..\Datadog.Profiler.Native.Windows\SymPdbParser.h"
#include "HResultConverter.h"
#endif

const std::string DebugInfoStore::NoFileFound = "";
const std::uint32_t DebugInfoStore::NoStartLine = 0;

DebugInfoStore::DebugInfoStore(ICorProfilerInfo4* profilerInfo, IConfiguration* _configuration) noexcept :
    _profilerInfo{profilerInfo},
    _isEnabled{_configuration->IsDebugInfoEnabled()}
{
}

SymbolDebugInfo DebugInfoStore::Get(ModuleID moduleId, mdMethodDef methodDef)
{
    if (!_isEnabled)
    {
        return {NoFileFound, NoStartLine};
    }

    std::unique_lock _l(_modulesMutex);

    auto it = _modulesInfo.find(moduleId);
    if (it == _modulesInfo.cend())
    {
        ParseModuleDebugInfo(moduleId);
    }

    ModuleDebugInfo& info = (it == _modulesInfo.cend()) ? _modulesInfo[moduleId] : it->second;

    // we should support 2 situations:
    //  - portable .pdb was found and we can use methodDef as RID
    //  - only windows .pdb was found and we have rebuilt the RID
    if ((info.LoadingState == SymbolLoadingState::Portable) || (info.LoadingState == SymbolLoadingState::Windows))
    {
        auto rid = RidFromToken(methodDef);
        return GetFromRID(info, moduleId, rid);
    }

    return {NoFileFound, NoStartLine};
}

void DebugInfoStore::ParseModuleDebugInfo(ModuleID moduleId)
{
    // This lookup creates an invalid ModuleInfo
    auto& moduleInfo = _modulesInfo[moduleId];
    moduleInfo.LoadingState = SymbolLoadingState::Unknown;

    fs::path filePath = GetModuleFilePath(moduleId);
    moduleInfo.ModulePath = filePath.string();

    if (!filePath.has_extension() || (filePath.extension() != ".dll" && filePath.extension() != ".exe"))
    {
        // An invalid entry has been created for this file
        Log::Debug("Unrecognized file path: ", filePath, ". No debug info will be retrieved for module ID", moduleId);
        return;
    }

    auto pdbFile = filePath.parent_path() / filePath.filename().replace_extension(".pdb");

    std::error_code ec;
    if (!fs::exists(pdbFile, ec))
    {
        // TODO: we may supply other path to search for the pdb file
        Log::Info("No PDB file (associated to module ", filePath, ")`", pdbFile.filename(), "` was found in ", filePath.parent_path());
        return;
    }

    Log::Debug("Parsing ", pdbFile, " pdb file. (for module ", filePath,")");

    ParseModuleDebugInfo(moduleId, pdbFile.string(), filePath.string(), moduleInfo);
}

void DebugInfoStore::ParseModuleDebugInfo(ModuleID moduleId, const std::string& pdbFilename, const std::string& moduleFilename, ModuleDebugInfo& moduleInfo)
{
    // first, try to load the symbols via Portable PDB
    if (TryLoadSymbolsWithPortable(pdbFilename, moduleFilename, moduleInfo))
    {
        Log::Debug("PDB file ", pdbFilename, " parsed successfully (for module ", moduleFilename, ")");
        return;
    }

#ifdef _WINDOWS
    // try to load the symbols via Windows PDB parsers as a fallback
    if (moduleInfo.LoadingState != SymbolLoadingState::Portable)
    {
        if (TryLoadSymbolsWithSym(moduleId, pdbFilename, moduleFilename, moduleInfo))
        {
            Log::Debug("PDB file ", pdbFilename, " parsed successfully with Sym (for module ", moduleFilename, ")");
            return;
        }
        if (TryLoadSymbolsWithDbgHelp(pdbFilename, moduleInfo))
        {
            Log::Debug("PDB file ", pdbFilename, " parsed successfully with DbgHelp (for module ", moduleFilename, ")");
        }
        else
        {
            moduleInfo.LoadingState = SymbolLoadingState::Failed;
            Log::Debug("Failed to parse debug info from ", pdbFilename, " with DbgHelp (for module ", moduleFilename, ")");
        }
    }
#else
    moduleInfo.LoadingState = SymbolLoadingState::Failed;
#endif
}

bool DebugInfoStore::TryLoadSymbolsWithPortable(const std::string& pdbFilename, const std::string& moduleFilename, ModuleDebugInfo& moduleInfo)
{
    try
    {
        auto r = PPDB::PortablePdbReader::CreateReader(pdbFilename.c_str());
        auto m = r->GetNamedEntry<PPDB::MetadataStreamReader>();
        auto dtTable = m->GetTableReader<PPDB::DocumentTableReader>();
        if (dtTable == nullptr)
        {
            Log::Warn("Unable to get the DocumentTable from the PDB file ", pdbFilename, ".");
            return false;
        }

        Log::Debug("Reading DocumentTable: ", dtTable->RowCount(), " document(s)");
        moduleInfo.Files.reserve(dtTable->RowCount() + 1); // + the special case described below
        // we add empty string for the first document at index 0
        // to handle the case where a symbol has no associated document
        moduleInfo.Files.push_back(NoFileFound);
        for (size_t i = 1; i <= dtTable->RowCount(); ++i)
        {
            PPDB::DocumentTableReader::Row row;
            dtTable->SetRow(i);
            dtTable->NextRow(row);

            moduleInfo.Files.push_back(row.Name);
        }

        auto mdiTable = m->GetTableReader<PPDB::MethodDebugInformationTableReader>();
        Log::Debug("Reading MethodDebugInformationTable: ", mdiTable->RowCount(), " row(s)");

        moduleInfo.RidToDebugInfo.reserve(mdiTable->RowCount() + 1);

        // Just in case a RID ended up to 0 due to a bug
        moduleInfo.RidToDebugInfo.push_back({NoFileFound, NoStartLine});
        for (size_t i = 1; i <= mdiTable->RowCount(); ++i)
        {
            PPDB::MethodDebugInformationTableReader::Row row;
            mdiTable->SetRow(i);
            mdiTable->NextRow(row);
            std::uint32_t startLine = 0;
            for (const auto& s : row.Points)
            {
                // Check if it's reasonable to get the first point with a valid StartLine
                if (s.StartLine != 0xfeefee && s.EndLine != 0xfeefee)
                {
                    startLine = s.StartLine;
                    break;
                }
            }
            moduleInfo.RidToDebugInfo.emplace_back() = {moduleInfo.Files[row.InitialDocument], startLine};
        }
        moduleInfo.LoadingState = SymbolLoadingState::Portable;

        // Log memory size of loaded symbols
        auto memorySize = moduleInfo.GetMemorySize();
        Log::Info("Loaded symbols from Portable PDB for module ", moduleFilename,
                  ". Memory size: ", memorySize, " bytes (",
                  moduleInfo.Files.size(), " files, ",
                  moduleInfo.RidToDebugInfo.size(), " methods)");

        return true;
    }
    catch (PPDB::Exception const& ec)
    {
        Log::Warn("Failed to parse debug info from ", pdbFilename,
                  ".(Module: ", moduleFilename, "Error name: ", ec.Name, ", code: ", std::hex, static_cast<std::uint32_t>(ec.Error), ", metadata table: ", static_cast<std::uint32_t>(ec.Table), ")");
    }
    catch (...)
    {
        Log::Warn("Unexpected error happened while parsing the pdb file (Module: ", moduleFilename, "): ", pdbFilename);
    }

    return false;
}


#ifdef _WINDOWS
bool DebugInfoStore::TryLoadSymbolsWithSym(ModuleID moduleId, const std::string& pdbFile, const std::string& moduleFile, ModuleDebugInfo& moduleInfo)
{
    // clear the module info in case some partial data was loaded
    moduleInfo.RidToDebugInfo.clear();

    moduleInfo.Files.clear();
    moduleInfo.Files.reserve(DEFAULT_RESERVE_SIZE);
    // still need to have the first file as empty string
    moduleInfo.Files.push_back(NoFileFound);

    // Get the IMetaDataImport from the ModuleID
    CComPtr<IMetaDataImport> pMetaDataImport;
    HRESULT hr = _profilerInfo->GetModuleMetaData(moduleId, CorOpenFlags::ofRead, IID_IMetaDataImport, reinterpret_cast<IUnknown**>(&pMetaDataImport));
    if (FAILED(hr))
    {
        Log::Debug("GetModuleMetaData() failed with HRESULT = ", HResultConverter::ToStringWithCode(hr));
        return false;
    }

    // the module LoadingState is set by the parser in case of success
    SymParser parser;
    bool success = parser.LoadPdbFile(pMetaDataImport, &moduleInfo, pdbFile, moduleFile);

    return success;
}

bool DebugInfoStore::TryLoadSymbolsWithDbgHelp(const std::string& pdbFile, ModuleDebugInfo& moduleInfo)
{
    // clear the module info in case some partial data was loaded
    moduleInfo.RidToDebugInfo.clear();

    moduleInfo.Files.clear();
    moduleInfo.Files.reserve(DEFAULT_RESERVE_SIZE);
    // still need to have the first file as empty string
    moduleInfo.Files.push_back(NoFileFound);

    // the module LoadingState is set by the parser in case of success
    DbgHelpParser parser(&moduleInfo);
    bool success = parser.LoadPdbFile(pdbFile);

    return success;
}
#endif

fs::path DebugInfoStore::GetModuleFilePath(ModuleID moduleId) const
{
    ULONG nameCharCount = 0;
    DWORD flags;
    // 2 steps way to get the assembly name (get the buffer size first and then fill it up with the name)
    auto hr = _profilerInfo->GetModuleInfo2(moduleId, 0, nameCharCount, &nameCharCount, nullptr, 0, &flags);
    if (FAILED(hr))
    {
        return {};
    }

    auto buffer = std::make_unique<WCHAR[]>(nameCharCount);
    hr = _profilerInfo->GetModuleInfo2(moduleId, 0, nameCharCount, &nameCharCount, buffer.get(), 0, &flags);
    // maybe check flags if the file is on disk or at least accessible
    if (FAILED(hr))
    {
        return {};
    }

    return buffer.get();
}