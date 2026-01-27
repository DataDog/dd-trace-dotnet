// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "DebugInfoStore.h"

#include <corsym.h>
#include <string>
#include<vector>
#include <windows.h>


struct SymMethodInfo
{
    uint32_t rid;
    std::string_view sourceFile;
    uint32_t lineNumber;
};

class SymParser
{
public:
    SymParser(ICorProfilerInfo4* pCorProfilerInfo, ModuleID moduleId, ModuleDebugInfo* pModuleInfo);
    ~SymParser();

    bool LoadPdbFile(const std::string& pdbFilePath, const std::string& moduleFilePath);
    std::vector<SymMethodInfo> GetMethods();

private:
    bool GetMetadataImport(ModuleID moduleId);
    bool GetSymReader(const std::string& moduleFilePath);
    bool ComputeMethodsInfo();
    bool GetMethodInfoFromSymbol(ISymUnmanagedMethod* pMethod, SymMethodInfo& info);
    std::string& FindOrAddSourceFile(const char* filePath);

private:
    // Hash functor for string_view to use as key in unordered_map
    struct StringViewHash
    {
        size_t operator()(std::string_view sv) const noexcept
        {
            // Use std::hash<std::string_view> if available (C++17)
            // For C++14, use a simple hash combination
            std::hash<std::string_view> hasher;
            return hasher(sv);
        }
    };

    // Equality functor for string_view
    struct StringViewEqual
    {
        bool operator()(std::string_view lhs, std::string_view rhs) const noexcept
        {
            return lhs == rhs;
        }
    };

private:
    ModuleID _moduleId;
    ICorProfilerInfo4* _pCorProfilerInfo;
    ModuleDebugInfo* _pModuleInfo;

    ISymUnmanagedReader* _pReader;
    IMetaDataImport* _pMetaDataImport;

    // strings corresponding to source file paths are stored in the given ModuleDebugInfo
    // but we use this map to avoid duplications
    std::unordered_map<std::string_view, std::string*, StringViewHash, StringViewEqual> _sourceFileMap;

    // this stores all the managed methods found in the PDB with string views to the source file paths
    // stored in the given ModuleDebugInfo
    std::vector<SymMethodInfo> _methods;

    std::string _guid;
    DWORD _age;
};