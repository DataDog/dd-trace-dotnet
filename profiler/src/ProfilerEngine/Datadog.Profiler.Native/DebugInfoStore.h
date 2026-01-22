// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

// from dotnet coreclr includes
#include "cor.h"
#include "corprof.h"
// end

#include "IDebugInfoStore.h"
#include "Log.h"

#include "shared/src/native-src/dd_filesystem.hpp"

#include <mutex>
#include <string>
#include <unordered_map>
#include <unordered_set>

class IConfiguration;

enum class SymbolLoadingState
{
    Unknown,
    Failed,
    Portable,
    Windows
};

struct ModuleDebugInfo
{
public:
    std::string ModulePath;

    // for both Portable and Windows PDBs, we need to keep track of the loaded files to avoid storing them multiple times
    std::vector<std::string> Files;

    // for Portable PDBs, we can use directly the RID from the methodDef token to lookup the debug info
    std::vector<SymbolDebugInfo> RidToDebugInfo;

    SymbolLoadingState LoadingState;
    bool ErrorLogged = false;
};


class DebugInfoStore : public IDebugInfoStore
{
public:
    static const std::string NoFileFound;
    static const std::uint32_t NoStartLine;

public:
    DebugInfoStore(ICorProfilerInfo4* profilerInfo, IConfiguration* configuration) noexcept;

    SymbolDebugInfo Get(ModuleID moduleId, mdMethodDef methodDef);

    // for tests
    void ParseModuleDebugInfo(std::string pdbFilename, std::string moduleFilename, ModuleDebugInfo& moduleInfo);

private:
    void ParseModuleDebugInfo(ModuleID moduleID);
    fs::path GetModuleFilePath(ModuleID moduleId) const;

    template <typename TInfo>
    SymbolDebugInfo GetFromRID(TInfo& info, ModuleID moduleId, RID rid)
    {
        auto invalidInfo =
            (info.LoadingState == SymbolLoadingState::Failed) ||
            (info.LoadingState == SymbolLoadingState::Unknown) ||
            (rid >= info.RidToDebugInfo.size());
        if (!invalidInfo)
        {
            return info.RidToDebugInfo[rid];
        }

        // log only once per module
        auto alreadyLogged = std::exchange(info.ErrorLogged, true);
        if (!alreadyLogged)
        {
            if (
                (info.LoadingState == SymbolLoadingState::Failed) ||
                (info.LoadingState == SymbolLoadingState::Unknown)
                )
            {
                Log::Info("The debug info for the module `", info.ModulePath, "` seems to be invalid");
            }
            else
            if (rid >= info.RidToDebugInfo.size())
            {
                Log::Info("The RID is out of the symbols array bounds (RID: ", rid, "). Number of debug info: ", info.RidToDebugInfo.size(),
                            ", module path: ", info.ModulePath);
            }
        }
        return SymbolDebugInfo{NoFileFound, NoStartLine};
    }

#ifdef _WINDOWS
    bool TryLoadSymbolsWithDbgHelp(std::string pdbFile, ModuleDebugInfo& moduleInfo);
#endif

    // we need to support both Portable PDB (Windows and Linux) and Windows PDB (Windows only for old .NET Framework scenarios)
    // - portable PDB: we can use directly the RID from the methodDef token to lookup the debug info
    // - windows PDB: we need to use the RVA to find the correct method
    // So, the per module debug info needs to store either one or the other
    std::unordered_map<ModuleID, ModuleDebugInfo> _modulesInfo;

    ICorProfilerInfo4* _profilerInfo;
    bool _isEnabled;
    std::mutex _modulesMutex;
};