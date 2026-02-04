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


class DbgHelpParser
{
public:
    DbgHelpParser(ModuleDebugInfo* pModuleInfo);
    ~DbgHelpParser();

    bool LoadPdbFile(const std::string& pdbFilePath);
    std::vector<MethodInfo> GetMethods();
    std::string GetGuid() const { return _guid; }
    DWORD GetAge() const { return _age; }

private:
    static BOOL CALLBACK EnumMethodSymbolsCallback(PSYMBOL_INFO pSymInfo, ULONG SymbolSize, PVOID UserContext);
    bool ComputeMethodsInfo();
    std::string_view FindOrAddSourceFile(const char* filePath);

private:
    ModuleDebugInfo* _pModuleInfo;

    HANDLE _hProcess;
    uint64_t _baseAddress;

    // the symbols are enumerated in an implicit "RID" order
    uint32_t _currentRID;

    // strings corresponding to source file paths are stored in the given ModuleDebugInfo
    // but we use this map to avoid duplications
    std::unordered_map<std::string_view, std::string*> _sourceFileMap;

    // this stores all the managed methods found in the PDB with string views to the source file paths
    // stored in the given ModuleDebugInfo
    std::vector<MethodInfo> _methods;

    std::string _guid;
    DWORD _age;

};

