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

    auto rid = RidFromToken(methodDef);
    auto it = _modulesInfo.find(moduleId);
    if (it != _modulesInfo.cend())
    {
        return Get(it->second, moduleId, rid);
    }

    ParseModuleDebugInfo(moduleId);

    auto& info = _modulesInfo[moduleId];
    return Get(info, moduleId, rid);
}

void DebugInfoStore::ParseModuleDebugInfo(ModuleID moduleId)
{
    // This lookup creates an invalid ModuleInfo
    auto& moduleInfo = _modulesInfo[moduleId];

    fs::path filePath = GetModuleFilePath(moduleId);
    if (!filePath.has_extension() || (filePath.extension() != ".dll" && filePath.extension() != ".exe"))
    {
        // An invalid entry has been created for this file
        Log::Debug("Unrecognized file path: ", filePath, ". No debug info will be retrieved for module ID", moduleId);
        return;
    }

    auto pdbFile = filePath.parent_path() / filePath.filename().replace_extension(".pdb");
    Log::Debug("Parsing ", pdbFile, " pdb file.");

    std::error_code ec;
    if (!fs::exists(pdbFile, ec))
    {
        // TODO: we may supply other path to search for the pdb file
        Log::Info("No PDB file `", pdbFile.filename(), "` was found in ", filePath.parent_path());
        return;
    }

    try
    {
        auto r = PPDB::PortablePdbReader::CreateReader(pdbFile.string().c_str());
        auto m = r->GetNamedEntry<PPDB::MetadataStreamReader>();
        auto dtTable = m->GetTableReader<PPDB::DocumentTableReader>();
        if (dtTable == nullptr)
        {
            Log::Warn("Unable to get the DocumentTable from the PDB file ", pdbFile, ".");
            return;
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

        moduleInfo.SymbolsDebugInfo.reserve(mdiTable->RowCount() + 1);

        // Just in case a RID ended up to 0 due to a bug
        moduleInfo.SymbolsDebugInfo.push_back({NoFileFound, NoStartLine});
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
            moduleInfo.SymbolsDebugInfo.emplace_back() = {moduleInfo.Files[row.InitialDocument], startLine};
        }
        moduleInfo.IsValid = true;
        Log::Debug("PDB file ", pdbFile, " parsed successfully");
    }
    catch (PPDB::Exception const& ec)
    {
        Log::Warn("Failed to parse debug info from ", pdbFile,
                  ".(Error name: ", ec.Name, ", code: ", std::hex, static_cast<std::uint32_t>(ec.Error), ", metadata table: ", static_cast<std::uint32_t>(ec.Table), ")");
    }
    catch (...)
    {
        Log::Warn("Unexpected error happened while parsing the pdb file: ", pdbFile);
    }
}

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