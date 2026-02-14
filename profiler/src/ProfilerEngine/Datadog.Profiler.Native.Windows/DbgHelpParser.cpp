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

// Helper struct to pass context to the static callback
struct CallbackContext
{
    DbgHelpParsingContext* context;
    ModuleDebugInfo* pModuleInfo;
    DbgHelpParser* parser;
};

bool DbgHelpParser::LoadPdbFile(ModuleDebugInfo* pModuleInfo, const std::string& pdbFilePath)
{
    // BUG? : dbghelp does not fail if the .pdb file does not exist...
    if (GetFileAttributesA(pdbFilePath.c_str()) == INVALID_FILE_ATTRIBUTES)
    {
        return false;
    }

    // Set up DbgHelp options
    DWORD options = SymGetOptions();
    //options |= SYMOPT_DEBUG;
    options |= SYMOPT_LOAD_LINES;           // Load line number information
    options |= SYMOPT_UNDNAME;              // Undecorate symbol names
    options |= SYMOPT_EXACT_SYMBOLS;        // Require exact symbol match
    options |= SYMOPT_FAIL_CRITICAL_ERRORS; // Don't show error dialogs
    SymSetOptions(options);

    HANDLE hProcess = GetCurrentProcess();
    if (!SymInitialize(hProcess, NULL, FALSE))
    {
        return false;
    }

    // Create temporary parsing context
    DbgHelpParsingContext context;
    context.hProcess = hProcess;
    context.baseAddress = 0;
    context.currentRID = 0;
    context.sourceFileMap.reserve(DEFAULT_RESERVE_SIZE);
    context.methods.reserve(DEFAULT_RESERVE_SIZE);

    // NOTE: don't forget to add the empty string in the map for files
    FindOrAddSourceFile("", pModuleInfo, context);

    // Load the PDB module
    context.baseAddress = SymLoadModuleEx(
        hProcess,
        NULL,
        pdbFilePath.c_str(),
        NULL,
        0x10000000, // arbitrary base address
        0,
        NULL,
        0
    );

    if (context.baseAddress == 0)
    {
        SymCleanup(hProcess);
        return false;
    }

    IMAGEHLP_MODULE64 moduleInfo = { 0 };
    moduleInfo.SizeOfStruct = sizeof(IMAGEHLP_MODULE64);
    if (!SymGetModuleInfo64(hProcess, context.baseAddress, &moduleInfo))
    {
        SymUnloadModule64(hProcess, context.baseAddress);
        SymCleanup(hProcess);
        return false;
    }

    // Compute method info
    if (!ComputeMethodsInfo(hProcess, context.baseAddress, pModuleInfo, context))
    {
        SymUnloadModule64(hProcess, context.baseAddress);
        SymCleanup(hProcess);
        return false;
    }

    // if no symbol was found, consider the pdb loading failed
    if (context.sourceFileMap.empty())
    {
        SymUnloadModule64(hProcess, context.baseAddress);
        SymCleanup(hProcess);
        return false;
    }

    // fill up the ModuleDebugInfo methods vector
    pModuleInfo->RidToDebugInfo.reserve(context.methods.size() + 1);

    // first element is for RID 0 which is not used
    pModuleInfo->RidToDebugInfo.push_back({NoFileFoundString, 0});

    // then insert all methods found in the .pdb
    for (const MethodInfo& sym : context.methods)
    {
        pModuleInfo->RidToDebugInfo.push_back({sym.sourceFile, sym.lineNumber});
    }

    pModuleInfo->LoadingState = SymbolLoadingState::Windows;

    // Log memory size of loaded symbols
    if (Log::IsDebugEnabled())
    {
        auto memorySize = pModuleInfo->GetMemorySize();
        Log::Debug("Loaded symbols from Windows PDB (DbgHelp) for ", pdbFilePath,
                  ". Memory size: ", memorySize, " bytes (",
                  pModuleInfo->Files.size(), " files, ",
                  pModuleInfo->RidToDebugInfo.size(), " methods)");
    }

    // Cleanup
    SymUnloadModule64(hProcess, context.baseAddress);
    SymCleanup(hProcess);

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
    CallbackContext* cbContext = reinterpret_cast<CallbackContext*>(UserContext);

    if (
        (pSymInfo->Tag == SymTagFunction)
        && ((pSymInfo->Flags & (SYMFLAG_CLR_TOKEN | SYMFLAG_METADATA)) == (SYMFLAG_CLR_TOKEN | SYMFLAG_METADATA))
        )
    {
        MethodInfo info;
        info.address = pSymInfo->Address;
        info.size = pSymInfo->Size;
        info.rid = cbContext->context->currentRID++;

        // Try to get source file and line information
        IMAGEHLP_LINE64 line = { 0 };
        line.SizeOfStruct = sizeof(IMAGEHLP_LINE64);
        DWORD displacement = 0;
        if (SymGetLineFromAddr64(cbContext->context->hProcess, pSymInfo->Address, &displacement, &line))
        {
            auto strSourceFile = line.FileName ? line.FileName : "";
            info.sourceFile = cbContext->parser->FindOrAddSourceFile(strSourceFile, cbContext->pModuleInfo, *cbContext->context);
            info.lineNumber = line.LineNumber;
        }
        else
        {
            info.sourceFile = NoFileFoundString;
            info.lineNumber = 0;
        }

        cbContext->context->methods.push_back(info);
    }

    return TRUE; // Continue enumeration
}

bool DbgHelpParser::ComputeMethodsInfo(HANDLE hProcess, uint64_t baseAddress, ModuleDebugInfo* pModuleInfo, DbgHelpParsingContext& context)
{
    // first RID is 1
    context.currentRID = 1;

    // the method symbols are enumerated in an implicit "RID" order corresponding
    // to the same order as in the metadata methodDef table
    // --> the rid will be the index in the enumeration
    CallbackContext cbContext = { &context, pModuleInfo, this };

    if (!SymEnumSymbols(
            hProcess,
            baseAddress,
            "*!*",  // Mask (all symbols)
            EnumMethodSymbolsCallback,
            &cbContext    // User context to pass to callback
    ))
    {
        return false;
    }

    return true;
}

std::string_view DbgHelpParser::FindOrAddSourceFile(const char* filePath, ModuleDebugInfo* pModuleInfo, DbgHelpParsingContext& context)
{
    // Use string_view as key to avoid creating std::string for lookup
    std::string_view key(filePath);

    // Try to find in the map
    auto map_it = context.sourceFileMap.find(key);
    if (map_it != context.sourceFileMap.end())
    {
        // Return view to existing string
        return *map_it->second;
    }

    // Not found - create new string and add to both containers
    // Using std::string's move constructor with emplace_back
    pModuleInfo->Files.emplace_back(filePath);
    std::string& new_str = pModuleInfo->Files.back();

    // Store pointer to the string in the map using string_view as key
    context.sourceFileMap.emplace(new_str, &new_str);

    return new_str;
}
