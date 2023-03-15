// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

// from dotnet coreclr includes
#include "cor.h"
#include "corprof.h"
// end

#include "IDebugInfoStore.h"

#include "shared/src/native-src/dd_filesystem.hpp"

#include <mutex>
#include <string>
#include <unordered_map>
#include <unordered_set>

class IConfiguration;

class DebugInfoStore : public IDebugInfoStore
{
public:
    DebugInfoStore(ICorProfilerInfo4* profilerInfo, IConfiguration* configuration);

    SymbolDebugInfo Get(ModuleID moduleId, mdMethodDef methodDef);

private:

    struct ModuleDebugInfo
    {
    public:
        std::vector<std::string> Files;
        std::vector<SymbolDebugInfo> SymbolsDebugInfo;
        bool IsValid = false;
    };

    void ParseModuleDebugInfo(ModuleID moduleID);
    fs::path GetModuleFilePath(ModuleID moduleId) const;

    static const std::string NoFileFound;
    static const std::uint32_t NoStartLine;

    std::unordered_map<ModuleID, ModuleDebugInfo> _modulesInfo;
    ICorProfilerInfo4* _profilerInfo;
    bool _isEnabled;
    std::mutex _modulesMutex;
};