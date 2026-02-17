// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <windows.h>

#include "DebugInfoStore.h"

// needed for symTag definitions
#define _NO_CVCONST_H
#include "DbgHelp.h"

#include <algorithm>
#include <string>
#include <unordered_map>
#include <vector>


struct MethodInfo
{
    uint32_t rid;
    uint64_t address;
    uint32_t size;
    std::string_view sourceFile;
    uint32_t lineNumber;
};

// Helper struct to hold temporary parsing state
struct DbgHelpParsingContext
{
    HANDLE hProcess;
    uint64_t baseAddress;
    uint32_t currentRID;
    std::unordered_map<std::string_view, std::string*> sourceFileMap;
    std::vector<MethodInfo> methods;
};

class DbgHelpParser
{
public:
    DbgHelpParser() = default;
    ~DbgHelpParser() = default;

    bool LoadPdbFile(ModuleDebugInfo* pModuleInfo, const std::string& pdbFilePath);

private:
    static BOOL CALLBACK EnumMethodSymbolsCallback(PSYMBOL_INFO pSymInfo, ULONG SymbolSize, PVOID UserContext);
    bool ComputeMethodsInfo(HANDLE hProcess, uint64_t baseAddress, ModuleDebugInfo* pModuleInfo, DbgHelpParsingContext& context);
    std::string_view FindOrAddSourceFile(const char* filePath, ModuleDebugInfo* pModuleInfo, DbgHelpParsingContext& context);

private:
    size_t DEFAULT_RESERVE_SIZE = 1024;
};

