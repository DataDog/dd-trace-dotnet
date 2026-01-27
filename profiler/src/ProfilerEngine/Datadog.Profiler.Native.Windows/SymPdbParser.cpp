// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "SymPdbParser.h"
#include "HResultConverter.h"
#include <atlbase.h>
#include <metahost.h>

// Link with ole32.lib for COM and mscoree.lib for metadata APIs
#pragma comment(lib, "ole32.lib")
#pragma comment(lib, "mscoree.lib")

// Define the GUIDs for ISymUnmanagedBinder and IMetaDataDispenser
#include <initguid.h>

// CLSID for CorSymBinder
// {0A29FF9E-7F9C-4437-8B11-F424491E3931}
DEFINE_GUID(CLSID_CorSymBinder_SxS,
            0x0A29FF9E, 0x7F9C, 0x4437, 0x8B, 0x11, 0xF4, 0x24, 0x49, 0x1E, 0x39, 0x31);

// IID for ISymUnmanagedBinder
// {AA544D42-28CB-11d3-BD22-0000F80849BD}
DEFINE_GUID(IID_ISymUnmanagedBinder,
            0xAA544D42, 0x28CB, 0x11d3, 0xBD, 0x22, 0x00, 0x00, 0xF8, 0x08, 0x49, 0xBD);


const std::string NoFileFoundString = "";

SymParser::SymParser(ICorProfilerInfo4* pCorProfilerInfo, ModuleID moduleId, ModuleDebugInfo* pModuleInfo)
    :
    _moduleId(moduleId),
    _pCorProfilerInfo(pCorProfilerInfo),
    _pModuleInfo(pModuleInfo),
    _pReader(nullptr),
    _pMetaDataImport(nullptr)
{
    _pCorProfilerInfo->AddRef();
    _sourceFileMap.reserve(1024);
    _methods.reserve(1024);

    // NOTE: don't forget to add the empty string in the map for files
    FindOrAddSourceFile("");
}

SymParser::~SymParser()
{
    _pCorProfilerInfo->Release();

    if (_pReader != nullptr)
    {
        _pReader->Release();
        _pReader = nullptr;
    }
    if (_pMetaDataImport != nullptr)
    {
        _pMetaDataImport->Release();
        _pMetaDataImport = nullptr;
    }
}

bool SymParser::LoadPdbFile(const std::string& pdbFilePath, const std::string& moduleFilePath)
{
    if (GetFileAttributesA(pdbFilePath.c_str()) == INVALID_FILE_ATTRIBUTES)
    {
        return false;
    }

    if (GetFileAttributesA(moduleFilePath.c_str()) == INVALID_FILE_ATTRIBUTES)
    {
        return false;
    }

    if (!GetMetadataImport(_moduleId))
    {
        return false;
    }

    if (!GetSymReader(moduleFilePath))
    {
        return false;
    }

    if (!ComputeMethodsInfo())
    {
        return false;
    }

        // if no symbol was found, consider the pdb loading failed
    if (_sourceFileMap.empty())
    {
        return false;
    }

    // fill up the methods vector
    _pModuleInfo->RidToDebugInfo.reserve(_methods.size() + 1);

    // first element is for RID 0 which is not used
    _pModuleInfo->RidToDebugInfo.push_back({NoFileFoundString, 0});

    // then insert all methods found in the .pdb
    for (const SymMethodInfo& sym : _methods)
    {
        _pModuleInfo->RidToDebugInfo.push_back({sym.sourceFile, sym.lineNumber});
    }

    return true;
}

const uint32_t LAST_METHODDEF_RID = 0x00010000;
bool SymParser::ComputeMethodsInfo()
{
    if (_pReader == nullptr)
    {
        return false;
    }

    HRESULT hr;

    // Use metadata API to enumerate method defs.
    // If it fails, try to enumerate "all" method tokens by hardcoding a range.
    // Since we don't have direct access to all methods, we'll try a range of common tokens
    // This is a workaround since ISymUnmanagedReader doesn't have a direct EnumMethods API
    ULONG cRows = 0;

    // Get IMetaDataTables interface to query the MethodDef table
    CComPtr<IMetaDataTables> pTables;
    hr = _pMetaDataImport->QueryInterface(IID_IMetaDataTables, (void**)&pTables);
    if (FAILED(hr) || pTables == nullptr)
    {
        cRows = LAST_METHODDEF_RID;
    }
    else
    {
        // Get the number of rows in the MethodDef table (table index 0x06 = Method)
        hr = pTables->GetTableInfo(
            0x06,   // MethodDef table
            NULL,   // cbRow (not needed)
            &cRows, // pcRows (number of methods)
            NULL,   // pcCols (not needed)
            NULL,   // piKey (not needed)
            NULL    // ppName (not needed)
        );

        if (FAILED(hr))
        {
            cRows = LAST_METHODDEF_RID;
        }
    }

    for (uint32_t rid = 1; rid < cRows; rid++)
    {
        mdMethodDef token = TokenFromRid(rid, mdtMethodDef);

        ISymUnmanagedMethod* pMethod = nullptr;
        hr = _pReader->GetMethod(token, &pMethod);
        if (SUCCEEDED(hr))
        {
            SymMethodInfo info;
            if (GetMethodInfoFromSymbol(pMethod, info))
            {
                _methods.push_back(info);
            }
            pMethod->Release();
        }
        else
        {
            // some methods don't have information
            SymMethodInfo info;
            info.rid = rid;
            info.lineNumber = 0;
            info.sourceFile = NoFileFoundString;

            // TODO: in case of hardcoded ranges, we could try to use
            //       the metatada API to check if the method exists
            //       and if not, stop the loop earlier
        }
    }

    // NOTE: methods are by design sorted by token

    return true;
}

bool SymParser::GetMethodInfoFromSymbol(ISymUnmanagedMethod* pMethod, SymMethodInfo& info)
{
    if (pMethod == nullptr)
    {
        return false;
    }

    // Get method token
    mdMethodDef token = 0;
    HRESULT hr = pMethod->GetToken(&token);
    if (FAILED(hr))
    {
        return false;
    }

    info.rid = RidFromToken(token);

    // Get sequence points (source line information)
    ULONG32 cPoints = 0;
    hr = pMethod->GetSequencePointCount(&cPoints);
    if (SUCCEEDED(hr) && (cPoints > 0))
    {
        cPoints = 1; // We only need the first sequence point for start line
        std::vector<ULONG32> offsets(cPoints);
        std::vector<ULONG32> lines(cPoints);
        std::vector<ULONG32> columns(cPoints);
        std::vector<ULONG32> endLines(cPoints);
        std::vector<ULONG32> endColumns(cPoints);
        std::vector<ISymUnmanagedDocument*> documents(cPoints);

        ULONG32 actualCount = 0;
        hr = pMethod->GetSequencePoints(
            cPoints,
            &actualCount,
            &offsets[0],
            &documents[0],
            &lines[0],
            &columns[0],
            &endLines[0],
            &endColumns[0]);

        if (SUCCEEDED(hr) && (actualCount > 0))
        {
            // Get the first sequence point's document and line
            ISymUnmanagedDocument* pDoc = documents[0];
            if (pDoc != nullptr)
            {
                // Get document URL (file path)
                ULONG32 urlLen = 0;
                hr = pDoc->GetURL(0, &urlLen, NULL);
                if (SUCCEEDED(hr) && (urlLen > 0))
                {
                    std::vector<WCHAR> url(urlLen);
                    hr = pDoc->GetURL(urlLen, &urlLen, &url[0]);
                    if (SUCCEEDED(hr))
                    {
                        // Convert wide string to UTF8 string
                        int len = WideCharToMultiByte(CP_UTF8, 0, &url[0], urlLen, NULL, 0, NULL, NULL);
                        std::string utf8Url(len, '\0');
                        WideCharToMultiByte(CP_UTF8, 0, &url[0], urlLen, &utf8Url[0], len, NULL, NULL);

                        std::string& sourceFile = FindOrAddSourceFile(utf8Url.c_str());
                        info.sourceFile = sourceFile;
                    }
                }

                // NOTE: 0xFEEFEE is a special value indicating "hidden lines"...
                info.lineNumber = lines[0];
                if (info.lineNumber == 0xFEEFEE)
                {
                    info.lineNumber = 0;
                }

                pDoc->Release();
            }
        }
    }

    if (info.sourceFile.empty())
    {
        info.sourceFile = NoFileFoundString;
        info.lineNumber = 0;
    }

    return true;
}


bool SymParser::GetSymReader(const std::string& moduleFilePath)
{
    // Create the symbol binder
    CComPtr<ISymUnmanagedBinder> pBinder;
    HRESULT hr = CoCreateInstance(
        CLSID_CorSymBinder_SxS,
        NULL,
        CLSCTX_INPROC_SERVER,
        IID_ISymUnmanagedBinder,
        (void**)&pBinder);

    if (FAILED(hr))
    {
        Log::Debug("Impossible to create CLSID_CorSymBinder_SxS with HRESULT = ", HResultConverter::ToStringWithCode(hr));
        return false;
    }

    int len = MultiByteToWideChar(CP_ACP, 0, moduleFilePath.c_str(), -1, NULL, 0);
    std::wstring wModulePath(len, L'\0');
    MultiByteToWideChar(CP_ACP, 0, moduleFilePath.c_str(), -1, &wModulePath[0], len);

    // Get symbol reader from the module file (not PDB) with metadata import
    // GetReaderForFile expects the module path and will automatically find the PDB
    hr = pBinder->GetReaderForFile(_pMetaDataImport, wModulePath.c_str(), nullptr, &_pReader);
    if (FAILED(hr))
    {
        Log::Debug("GetReaderForFile() failed with HRESULT = ", HResultConverter::ToStringWithCode(hr));
        return false;
    }

    return true;
}

bool SymParser::GetMetadataImport(ModuleID moduleId)
{
    HRESULT hr = _pCorProfilerInfo->GetModuleMetaData(moduleId, CorOpenFlags::ofRead, IID_IMetaDataImport, reinterpret_cast<IUnknown**>(&_pMetaDataImport));
    if (FAILED(hr))
    {
        Log::Debug("GetModuleMetaData() failed with HRESULT = ", HResultConverter::ToStringWithCode(hr));
        return false;
    }

    return true;
}

std::vector<SymMethodInfo> SymParser::GetMethods()
{
    return _methods;
}

std::string& SymParser::FindOrAddSourceFile(const char* filePath)
{
    // Use string_view as key to avoid creating std::string for lookup
    std::string_view key(filePath);

    // Try to find in the map
    auto map_it = _sourceFileMap.find(key);
    if (map_it != _sourceFileMap.end())
    {
        // Return reference to existing string
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
