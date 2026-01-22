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
    uint32_t RVA;
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
    ModuleDebugInfo* _pModuleInfo;

    HANDLE _hProcess;
    uint64_t _baseAddress;

    // strings corresponding to source file paths are stored in the given ModuleDebugInfo
    // but we use this map to avoid duplications
    std::unordered_map<std::string_view, std::string*, StringViewHash, StringViewEqual> _sourceFileMap;

    // this stores all the managed methods found in the PDB with string views to the source file paths
    // stored in the given ModuleDebugInfo
    std::vector<MethodInfo> _methods;

    std::string _guid;
    DWORD _age;

};

