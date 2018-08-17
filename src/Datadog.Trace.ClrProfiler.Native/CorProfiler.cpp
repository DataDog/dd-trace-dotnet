// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#include <string>
#include <fstream>
#include <vector>
#include "CorProfiler.h"
#include "Macros.h"
#include "ComPtr.h"
#include "ModuleMetadata.h"
#include "ILRewriter.h"
#include "MetadataBuilder.h"
#include "IntegrationLoader.h"

// Note: Generally you should not have a single, global callback implementation, as that
// prevents your profiler from analyzing multiply loaded in-process side-by-side CLRs.
// However, this profiler implements the "profile-first" alternative of dealing with
// multiple in-process side-by-side CLR instances. First CLR to try to load us into this
// process wins; so there can only be one callback implementation created. (See
// ProfilerCallback::CreateObject.)
CorProfiler* g_pCallbackObject = nullptr;

// TODO: fix log path, read from config?
std::wofstream g_wLogFile;
std::string g_wszLogFilePath = "C:\\temp\\CorProfiler.log";

HRESULT STDMETHODCALLTYPE CorProfiler::Initialize(IUnknown* pICorProfilerInfoUnk)
{
    bIsAttached = FALSE;

    /*
    WCHAR wszTempDir[MAX_PATH] = { L'\0' };

    if (FAILED(GetEnvironmentVariable(L"ProgramData", wszTempDir, _countof(wszTempDir))))
    {
        return E_FAIL;
    }

    if (wcscpy_s(g_wszLogFilePath, _countof(g_wszLogFilePath), wszTempDir) != 0)
    {
        return E_FAIL;
    }

    if (wcscat_s(g_wszLogFilePath, _countof(g_wszLogFilePath), L"\\Datadog\\logs\\CorProfiler.log"))
    {
        return E_FAIL;
    }

    if (wcscpy_s(g_wszLogFilePath, _countof(g_wszLogFilePath), L"C:\\temp\\CorProfiler.log") != 0)
    {
        LOG_APPEND(L"Failed to attach profiler: could not copy log file path.");
        return E_FAIL;
    }
    */

    WCHAR integration_file_path[MAX_PATH]{};
    const DWORD integration_file_path_length = GetEnvironmentVariable(L"DATADOG_INTEGRATIONS", integration_file_path, _countof(integration_file_path));

    if (integration_file_path_length > 0)
    {
        LOG_APPEND(L"loading integrations from " << integration_file_path);
        all_integrations = IntegrationLoader::load_integrations_from_file(integration_file_path);
    }

    WCHAR processNames[MAX_PATH]{};
    const DWORD processNamesLength = GetEnvironmentVariable(L"DATADOG_PROFILER_PROCESSES", processNames, _countof(processNames));

    if (processNamesLength == 0)
    {
        LOG_APPEND(L"Failed to attach profiler: could not get DATADOG_PROFILER_PROCESSES environment variable.");
        return E_FAIL;
    }

    LOG_APPEND(L"DATADOG_PROFILER_PROCESSES = " << processNames);

    WCHAR currentProcessPath[MAX_PATH]{};
    const DWORD currentProcessPathLength = GetModuleFileName(nullptr, currentProcessPath, _countof(currentProcessPath));

    if (currentProcessPathLength == 0)
    {
        LOG_APPEND(L"Failed to attach profiler: could not get current module filename.");
        return E_FAIL;
    }

    LOG_APPEND(L"Module file name = " << currentProcessPath);

    WCHAR* lastSeparator = wcsrchr(currentProcessPath, L'\\');
    WCHAR* processName = lastSeparator == nullptr ? currentProcessPath : lastSeparator + 1;

    if (wcsstr(processNames, processName) == nullptr)
    {
        LOG_APPEND(L"Profiler disabled: module name \"" << processName << "\" does not match DATADOG_PROFILER_PROCESSES environment variable.");
        return E_FAIL;
    }

    HRESULT hr = pICorProfilerInfoUnk->QueryInterface<ICorProfilerInfo3>(&this->corProfilerInfo);
    LOG_IFFAILEDRET(hr, L"Profiler disabled: interface ICorProfilerInfo3 or higher not found.");

    const DWORD eventMask = COR_PRF_MONITOR_JIT_COMPILATION |
                            COR_PRF_DISABLE_TRANSPARENCY_CHECKS_UNDER_FULL_TRUST | /* helps the case where this profiler is used on Full CLR */
                            // COR_PRF_DISABLE_INLINING |
                            COR_PRF_MONITOR_MODULE_LOADS |
                            //COR_PRF_MONITOR_ASSEMBLY_LOADS |
                            //COR_PRF_MONITOR_APPDOMAIN_LOADS |
                            // COR_PRF_ENABLE_REJIT |
                            COR_PRF_DISABLE_ALL_NGEN_IMAGES;

    hr = this->corProfilerInfo->SetEventMask(eventMask);
    LOG_IFFAILEDRET(hr, L"Failed to attach profiler: unable to set event mask.");

    // we're in!
    LOG_APPEND(L"Profiler attached to process " << processName);
    this->corProfilerInfo->AddRef();
    bIsAttached = true;
    g_pCallbackObject = this;
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfiler::ModuleLoadFinished(ModuleID moduleId, HRESULT hrStatus)
{
    LPCBYTE pbBaseLoadAddr;
    WCHAR wszModulePath[MAX_PATH];
    ULONG cchNameOut;
    AssemblyID assemblyId;
    DWORD dwModuleFlags;

    HRESULT hr = this->corProfilerInfo->GetModuleInfo2(moduleId,
                                                       &pbBaseLoadAddr,
                                                       _countof(wszModulePath),
                                                       &cchNameOut,
                                                       wszModulePath,
                                                       &assemblyId,
                                                       &dwModuleFlags);

    LOG_IFFAILEDRET(hr, L"GetModuleInfo2 failed for ModuleID = " << HEX(moduleId));

    if ((dwModuleFlags & COR_PRF_MODULE_WINDOWS_RUNTIME) != 0)
    {
        // Ignore any Windows Runtime modules.  We cannot obtain writeable metadata
        // interfaces on them or instrument their IL
        return S_OK;
    }

    WCHAR assemblyName[512]{};
    ULONG assemblyNameLength = 0;
    hr = this->corProfilerInfo->GetAssemblyInfo(assemblyId, _countof(assemblyName), &assemblyNameLength, assemblyName, nullptr, nullptr);
    LOG_IFFAILEDRET(hr, L"Failed to get assembly name.");

    std::vector<integration> enabledIntegrations;

    // check if we need to instrument anything in this assembly,
    // for each integration...
    for (const auto& integration : all_integrations)
    {
        // TODO: check if integration is enabled in config
        for (const auto& method_replacement : integration.method_replacements)
        {
            if (method_replacement.target_method.assembly_name == std::wstring(assemblyName))
            {
                enabledIntegrations.push_back(integration);
            }
        }
    }

    if (enabledIntegrations.empty())
    {
        // we don't need to instrument anything in this module, skip it
        return S_OK;
    }

    LOG_APPEND(L"ModuleLoadFinished for " << assemblyName << ". Emitting instrumentation metadata.");

    ComPtr<IUnknown> metadataInterfaces;

    hr = this->corProfilerInfo->GetModuleMetaData(moduleId,
                                                  ofRead | ofWrite,
                                                  IID_IMetaDataImport,
                                                  metadataInterfaces.GetAddressOf());

    LOG_IFFAILEDRET(hr, L"Failed to get metadata interface.");

    const auto metadataImport = metadataInterfaces.As<IMetaDataImport>(IID_IMetaDataImport);
    const auto metadataEmit = metadataInterfaces.As<IMetaDataEmit>(IID_IMetaDataEmit);
    const auto assemblyImport = metadataInterfaces.As<IMetaDataAssemblyImport>(IID_IMetaDataAssemblyImport);
    const auto assemblyEmit = metadataInterfaces.As<IMetaDataAssemblyEmit>(IID_IMetaDataAssemblyEmit);

    mdModule module;
    hr = metadataImport->GetModuleFromScope(&module);
    LOG_IFFAILEDRET(hr, L"Failed to get module token.");

    ModuleMetadata* moduleMetadata = new ModuleMetadata(metadataImport,
                                                        assemblyName,
                                                        enabledIntegrations);

    MetadataBuilder metadataBuilder(*moduleMetadata,
                                    module,
                                    metadataImport,
                                    metadataEmit,
                                    assemblyImport,
                                    assemblyEmit);

    // emit a metadata reference to our assembly, Datadog.Trace.ClrProfiler.Managed
    BYTE rgbPublicKeyToken[] = { 0xde, 0xf8, 0x6d, 0x06, 0x1d, 0x0d, 0x2e, 0xeb };
    WCHAR wszLocale[] = L"neutral";

    ASSEMBLYMETADATA assemblyMetaData{};
    assemblyMetaData.usMajorVersion = 0;
    assemblyMetaData.usMinorVersion = 2;
    assemblyMetaData.usBuildNumber = 0;
    assemblyMetaData.usRevisionNumber = 0;
    assemblyMetaData.szLocale = wszLocale;
    assemblyMetaData.cbLocale = _countof(wszLocale);

    mdAssemblyRef assemblyRef;
    hr = metadataBuilder.emit_assembly_ref(L"Datadog.Trace.ClrProfiler.Managed",
                                           assemblyMetaData,
                                           rgbPublicKeyToken,
                                           _countof(rgbPublicKeyToken),
                                           assemblyRef);

    RETURN_OK_IF_FAILED(hr);

    for (const auto& integration : enabledIntegrations)
    {
        for (const auto& method_replacement : integration.method_replacements)
        {
            // for each method replacement in each enabled integration,
            // emit a reference to the instrumentation wrapper methods
            hr = metadataBuilder.store_wrapper_method_ref(method_replacement);
            RETURN_OK_IF_FAILED(hr);
        }
    }

    // store module info for later lookup
    m_moduleIDToInfoMap.Update(moduleId, moduleMetadata);
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfiler::ModuleUnloadFinished(ModuleID moduleId, HRESULT hrStatus)
{
    ModuleMetadata* metadata;

    if (m_moduleIDToInfoMap.LookupIfExists(moduleId, &metadata))
    {
        m_moduleIDToInfoMap.Erase(moduleId);
        delete metadata;
    }

    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfiler::JITCompilationStarted(FunctionID functionId, BOOL fIsSafeToBlock)
{
    ClassID classId;
    ModuleID moduleId;
    mdToken functionToken = mdTokenNil;

    HRESULT hr = this->corProfilerInfo->GetFunctionInfo(functionId, &classId, &moduleId, &functionToken);
    RETURN_OK_IF_FAILED(hr);

    ModuleMetadata* moduleMetadata = nullptr;

    if (!m_moduleIDToInfoMap.LookupIfExists(moduleId, &moduleMetadata))
    {
        // we haven't stored a ModuleInfo for this module, so we can't modify its IL
        return S_OK;
    }

    const int string_size = 1024;

    // get function name
    mdTypeDef caller_type_def = mdTypeDefNil;
    WCHAR caller_method_name[string_size]{};
    ULONG caller_method_name_length = 0;
    hr = moduleMetadata->metadata_import->GetMemberProps(functionToken, &caller_type_def, caller_method_name, string_size, &caller_method_name_length, nullptr, nullptr, nullptr, nullptr, nullptr, nullptr, nullptr, nullptr);
    RETURN_OK_IF_FAILED(hr);

    // get type name
    WCHAR caller_type_name[string_size]{};
    ULONG caller_type_name_length = 0;
    hr = moduleMetadata->metadata_import->GetTypeDefProps(caller_type_def, caller_type_name, string_size, &caller_type_name_length, nullptr, nullptr);
    RETURN_OK_IF_FAILED(hr);

    // check if we need to replace any methods called from this method
    for (const auto& integration : moduleMetadata->integrations)
    {
        for (const auto& method_replacement : integration.method_replacements)
        {
            // check known callers for IL opcodes that call into the target method.
            // if found, replace with calls to the instrumentation wrapper (wrapper_method_ref)
            if ((method_replacement.caller_method.type_name.empty() || method_replacement.caller_method.type_name == caller_type_name) &&
                (method_replacement.caller_method.method_name.empty() || method_replacement.caller_method.method_name == caller_method_name))
            {
                const auto& wrapper_method_key = method_replacement.wrapper_method.get_method_cache_key();
                mdMemberRef wrapper_method_ref = mdMemberRefNil;

                if (!moduleMetadata->TryGetWrapperMemberRef(wrapper_method_key, wrapper_method_ref))
                {
                    // no method ref token found for wrapper method, we can't do the replacement,
                    // this should never happen because we always try to add the method ref in ModuleLoadFinished()
                    // TODO: log this
                    return S_OK;
                }

                ILRewriter rewriter(this->corProfilerInfo, nullptr, moduleId, functionToken);

                // hr = rewriter.Initialize();
                hr = rewriter.Import();
                RETURN_OK_IF_FAILED(hr);

                bool modified = false;

                // for each IL instruction
                for (ILInstr* pInstr = rewriter.GetILList()->m_pNext;
                     pInstr != rewriter.GetILList();
                     pInstr = pInstr->m_pNext)
                {
                    // if its opcode is CALL or CALLVIRT
                    if ((pInstr->m_opcode == CEE_CALL || pInstr->m_opcode == CEE_CALLVIRT) &&
                        (TypeFromToken(pInstr->m_Arg32) == mdtMemberRef || TypeFromToken(pInstr->m_Arg32) == mdtMethodDef))
                    {
                        WCHAR target_method_name[string_size]{};
                        ULONG target_method_name_length = 0;

                        WCHAR target_type_name[string_size]{};
                        ULONG target_type_name_length = 0;

                        mdMethodDef target_method_def = mdMethodDefNil;
                        mdTypeDef target_type_def = mdTypeDefNil;

                        if (TypeFromToken(pInstr->m_Arg32) == mdtMemberRef)
                        {
                            // get function name from mdMemberRef
                            mdToken token = mdTokenNil;
                            hr = moduleMetadata->metadata_import->GetMemberRefProps(pInstr->m_Arg32, &token, target_method_name, string_size, &target_method_name_length, nullptr, nullptr);
                            RETURN_OK_IF_FAILED(hr);

                            if (method_replacement.target_method.method_name != target_method_name)
                            {
                                // method name doesn't match, skip to next instruction
                                continue;
                            }

                            // determine how to get type name from token, depending on the token type
                            if (TypeFromToken(token) == mdtTypeRef)
                            {
                                hr = moduleMetadata->metadata_import->GetTypeRefProps(token, nullptr, target_type_name, string_size, &target_type_name_length);
                                RETURN_OK_IF_FAILED(hr);
                                goto compare_type_and_method_names;
                            }

                            if (TypeFromToken(token) == mdtTypeDef)
                            {
                                target_type_def = token;
                                goto use_type_def;
                            }

                            if (TypeFromToken(token) == mdtMethodDef)
                            {
                                // we got an mdMethodDef back, so jump to where we use a methodDef instead of a methodRef
                                target_method_def = token;
                                goto use_method_def;
                            }

                            // value of token is not a supported token type, skip to next instruction
                            continue;
                        }

                        // if pInstr->m_Arg32 wasn't an mdtMemberRef, it must be an mdtMethodDef
                        target_method_def = pInstr->m_Arg32;

                    use_method_def:
                        // get function name from mdMethodDef
                        hr = moduleMetadata->metadata_import->GetMethodProps(target_method_def, &target_type_def, target_method_name, string_size, &target_method_name_length, nullptr, nullptr, nullptr, nullptr, nullptr);
                        RETURN_OK_IF_FAILED(hr);

                        if (method_replacement.target_method.method_name != target_method_name)
                        {
                            // method name doesn't match, skip to next instruction
                            continue;
                        }

                    use_type_def:
                        // get type name from mdTypeDef
                        hr = moduleMetadata->metadata_import->GetTypeDefProps(target_type_def, target_type_name, string_size, &target_type_name_length, nullptr, nullptr);
                        RETURN_OK_IF_FAILED(hr);

                    compare_type_and_method_names:
                        // if the target matches by type name and method name
                        if (method_replacement.target_method.type_name == target_type_name &&
                            method_replacement.target_method.method_name == target_method_name)
                        {
                            // replace with a call to the instrumentation wrapper
                            pInstr->m_opcode = CEE_CALL;
                            pInstr->m_Arg32 = wrapper_method_ref;

                            modified = true;
                        }
                    }
                }

                if (modified)
                {
                    hr = rewriter.Export();
                    return S_OK;
                }
            }
        }
    }

    return S_OK;
}

bool CorProfiler::IsAttached() const
{
    return bIsAttached;
}
