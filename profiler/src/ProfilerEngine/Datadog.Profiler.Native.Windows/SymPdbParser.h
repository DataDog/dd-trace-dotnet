// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "DebugInfoStore.h"

#include <atlbase.h>
#include <corsym.h>
#include <string>
#include <unordered_map>
#include <vector>
#include <windows.h>


struct SymMethodInfo
{
    uint32_t rid;
    std::string_view sourceFile;
    uint32_t lineNumber;
};

// Helper struct to hold temporary parsing state
struct SymParsingContext
{
    std::unordered_map<std::string_view, std::string_view> sourceFileMap;
    std::vector<SymMethodInfo> methods;
};

class SymParser
{
public:
    SymParser() = default;
    ~SymParser() = default;

    bool LoadPdbFile(IMetaDataImport* pMetaDataImport, ModuleDebugInfo* pModuleInfo, const std::string& pdbFilePath, const std::string& moduleFilePath);

private:
    size_t DEFAULT_RESERVE_SIZE = 1024;

private:
    bool GetSymReader(IMetaDataImport* pMetaDataImport, const std::string& moduleFilePath, CComPtr<ISymUnmanagedReader>& pReader);
    bool ComputeMethodsInfo(IMetaDataImport* pMetaDataImport, ISymUnmanagedReader* pReader, ModuleDebugInfo* pModuleInfo, SymParsingContext& context);
    bool GetMethodInfoFromSymbol(ISymUnmanagedMethod* pMethod, ModuleDebugInfo* pModuleInfo, SymParsingContext& context, SymMethodInfo& info);
    std::string_view FindOrAddSourceFile(const char* filePath, ModuleDebugInfo* pModuleInfo, SymParsingContext& context);
};