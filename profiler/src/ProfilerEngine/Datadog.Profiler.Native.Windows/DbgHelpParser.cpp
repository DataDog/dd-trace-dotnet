// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "DbgHelpParser.h"

#include <algorithm>
#include <sstream>
#include <string>

// No need to dynamically load dbghelp.dll, link directly because it is always available on Windows
// Link with dbghelp.lib
#pragma comment(lib, "dbghelp.lib")

const std::string NoFileFoundString = "";


DbgHelpParser::DbgHelpParser(ModuleDebugInfo* pModuleInfo)
    :
    _pModuleInfo(pModuleInfo),
    _baseAddress(0),
    _age(0)
{
    _sourceFileMap.reserve(1024);
    _methods.reserve(1024);

    // NOTE: don't forget to add the empty string in the map for files
    FindOrAddSourceFile("");

    DWORD options = SymGetOptions();
    //options |= SYMOPT_DEBUG;
    options |= SYMOPT_LOAD_LINES;           // Load line number information
    options |= SYMOPT_UNDNAME;              // Undecorate symbol names
    options |= SYMOPT_EXACT_SYMBOLS;        // Require exact symbol match
    options |= SYMOPT_FAIL_CRITICAL_ERRORS; // Don't show error dialogs
    SymSetOptions(options);

    _hProcess = GetCurrentProcess();
    if (!SymInitialize(_hProcess, NULL, FALSE))
    {
        _hProcess = NULL;
    }
}

DbgHelpParser::~DbgHelpParser()
{
    if (_hProcess != NULL)
    {
        if (_baseAddress != 0)
        {
            SymUnloadModule64(_hProcess, _baseAddress);
            _baseAddress = 0;
        }

        SymCleanup(_hProcess);
        _hProcess = NULL;
    }
}

bool DbgHelpParser::LoadPdbFile(const std::string& pdbFilePath)
{
    if (_hProcess == NULL)
    {
        return false;
    }

    // BUG? : dbghelp does not fail if the .pdb file does not exist...
    if (GetFileAttributesA(pdbFilePath.c_str()) == INVALID_FILE_ATTRIBUTES)
    {
        return false;
    }

    _baseAddress = SymLoadModuleEx(
        _hProcess,
        NULL,
        pdbFilePath.c_str(),
        NULL,
        0x10000000, // arbitrary base address
        0,
        NULL,
        0
    );

    if (_baseAddress == 0)
    {
        return false;
    }

    IMAGEHLP_MODULE64 moduleInfo = { 0 };
    moduleInfo.SizeOfStruct = sizeof(IMAGEHLP_MODULE64);
    if (!SymGetModuleInfo64(_hProcess, _baseAddress, &moduleInfo))
    {
        return false;
    }

    // TODO: remove if not needed
    _age = moduleInfo.PdbAge;
    GUID guid = moduleInfo.PdbSig70;
    char strGUID[80];
    sprintf_s(strGUID, 80, "%08x%04x%04x%02x%02x%02x%02x%02x%02x%02x%02x",
        guid.Data1, guid.Data2, guid.Data3,
        guid.Data4[0], guid.Data4[1], guid.Data4[2], guid.Data4[3],
        guid.Data4[4], guid.Data4[5], guid.Data4[6], guid.Data4[7]
        );
    _guid = strGUID;

    // Compute method info
    if (!ComputeMethodsInfo())
    {
        return false;
    }

    // if no symbol was found, consider the pdb loading failed
    if (_sourceFileMap.empty())
    {
        return false;
    }

    // fill up the ModuleDebugInfo methods vector
    _pModuleInfo->RidToDebugInfo.reserve(_methods.size() + 1);

    // first element is for RID 0 which is not used
    _pModuleInfo->RidToDebugInfo.push_back({NoFileFoundString, 0});

    // then insert all methods found in the .pdb
    for (const MethodInfo& sym : _methods)
    {
        _pModuleInfo->RidToDebugInfo.push_back({sym.sourceFile, sym.lineNumber});
    }

    _pModuleInfo->LoadingState = SymbolLoadingState::Windows;

    // Log memory size of loaded symbols
    auto memorySize = _pModuleInfo->GetMemorySize();
    Log::Info("Loaded symbols from Windows PDB (DbgHelp) for ", pdbFilePath,
              ". Memory size: ", memorySize, " bytes (",
              _pModuleInfo->Files.size(), " files, ",
              _pModuleInfo->RidToDebugInfo.size(), " methods)");

    return true;
}

uint32_t ExtractRID(const char* methodName)
{
    // methodName is expected to be in the format "Method.#<RID>" with the RID in base 16
    const char* ridStr = strstr(methodName, ".#");
    if (ridStr != nullptr)
    {
        return static_cast<uint32_t>(std::stoul(ridStr + 2, nullptr, 16));
    }

    return 0;
}

BOOL CALLBACK DbgHelpParser::EnumMethodSymbolsCallback(PSYMBOL_INFO pSymInfo, ULONG SymbolSize, PVOID UserContext)
{
    DbgHelpParser* parser = reinterpret_cast<DbgHelpParser*>(UserContext);

    if (
        (pSymInfo->Tag == SymTagFunction)
        && ((pSymInfo->Flags & (SYMFLAG_CLR_TOKEN | SYMFLAG_METADATA)) == (SYMFLAG_CLR_TOKEN | SYMFLAG_METADATA))
        )
    {
        MethodInfo info;
        info.address = pSymInfo->Address;
        info.size = pSymInfo->Size;
        info.rid = parser->_currentRID++;

        // Try to get source file and line information
        IMAGEHLP_LINE64 line = { 0 };
        line.SizeOfStruct = sizeof(IMAGEHLP_LINE64);
        DWORD displacement = 0;
        if (SymGetLineFromAddr64(parser->_hProcess, pSymInfo->Address, &displacement, &line))
        {
            auto strSourceFile = line.FileName ? line.FileName : "";
            info.sourceFile = parser->FindOrAddSourceFile(strSourceFile);
            info.lineNumber = line.LineNumber;
        }
        else
        {
            info.sourceFile = NoFileFoundString;
            info.lineNumber = 0;
        }

        parser->_methods.push_back(info);
    }

    return TRUE; // Continue enumeration
}

bool DbgHelpParser::ComputeMethodsInfo()
{
    // first RID is 1
    _currentRID = 1;

    // the method symbols are enumerated in an implicit "RID" order corresponding
    // to the same order as in the metadata methodDef table
    // --> the rid will be the index in the enumeration
    if (!SymEnumSymbols(
            _hProcess,
            _baseAddress,
            "*!*",  // Mask (all symbols)
            EnumMethodSymbolsCallback,
            this    // User context to store the methods in _methods instance field
    ))
    {
        return false;
    }

    return true;
}

std::string_view DbgHelpParser::FindOrAddSourceFile(const char* filePath)
{
    // Use string_view as key to avoid creating std::string for lookup
    std::string_view key(filePath);

    // Try to find in the map
    auto map_it = _sourceFileMap.find(key);
    if (map_it != _sourceFileMap.end())
    {
        // Return view to existing string
        return *map_it->second;
    }

    // Not found - create new string and add to both containers
    // Using std::string's move constructor with emplace_back
    _pModuleInfo->Files.emplace_back(filePath);
    std::string& new_str = _pModuleInfo->Files.back();

    // Store pointer to the string in the map using string_view as key
    _sourceFileMap.emplace(new_str, &new_str);

    return new_str;
}


// TODO: remove the code used to keep track of the methods
std::vector<MethodInfo> DbgHelpParser::GetMethods()
{
    return _methods;
}
