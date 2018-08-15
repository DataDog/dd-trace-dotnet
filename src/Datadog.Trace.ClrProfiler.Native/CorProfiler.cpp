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
#include "ILRewriterWrapper.h"
#include "MetadataBuilder.h"

// Note: Generally you should not have a single, global callback implementation, as that
// prevents your profiler from analyzing multiply loaded in-process side-by-side CLRs.
// However, this profiler implements the "profile-first" alternative of dealing with
// multiple in-process side-by-side CLR instances. First CLR to try to load us into this
// process wins; so there can only be one callback implementation created. (See
// ProfilerCallback::CreateObject.)
CorProfiler* g_pCallbackObject = nullptr;

// TODO: fix log path, read from config?
std::wofstream g_wLogFile;
WCHAR g_wszLogFilePath[MAX_PATH] = L"C:\\temp\\CorProfiler.log";

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
    for (auto& integration : all_integrations)
    {
        // TODO: check if integration is enabled in config
        if (integration.target_assembly_name == std::wstring(assemblyName))
        {
            enabledIntegrations.push_back(integration);
        }
    }

    if (enabledIntegrations.empty())
    {
        LOG_APPEND(L"SKIPPING " << assemblyName);
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
    assemblyMetaData.usMajorVersion = 1;
    assemblyMetaData.usMinorVersion = 0;
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

    RETURN_IF_FAILED(hr);

    for (const auto& integration : enabledIntegrations)
    {
        hr = metadataBuilder.find_methods(integration);
        LOG_IFFAILEDRET(hr, L"Failed to find methods.");

        for (const auto& method_replacement : integration.method_replacements)
        {
            // for each method replacement in each enabled integration,
            // emit a reference to the instrumentation wrapper methods
            hr = metadataBuilder.store_wrapper_method_ref(integration, method_replacement);
            RETURN_IF_FAILED(hr);
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
    mdToken functionToken;

    HRESULT hr = this->corProfilerInfo->GetFunctionInfo(functionId, &classId, &moduleId, &functionToken);
    RETURN_IF_FAILED(hr);

    ModuleMetadata* moduleMetadata = nullptr;

    if (!m_moduleIDToInfoMap.LookupIfExists(moduleId, &moduleMetadata))
    {
        // we haven't stored a ModuleInfo for this module, so we can't modify its IL
        return S_OK;
    }

    // check if we need to replace any methods called from this method
    for (const auto& integration : moduleMetadata->m_Integrations)
    {
        for (const auto& method_replacement : integration.method_replacements)
        {
            // check known callers (caller_method_token) for IL opcodes that call
            // into the target method (target_method_token) and replace them
            // with called to the instrumentation wrapper (wrapper_method_ref)
            if (functionToken == method_replacement.caller_method_token)
            {
                const std::wstring wrapper_method_key = integration.get_wrapper_method_key(method_replacement);
                mdMemberRef wrapper_method_ref = mdMemberRefNil;

                if (moduleMetadata->TryGetWrapperMemberRef(wrapper_method_key, wrapper_method_ref))
                {
                    ILRewriter rewriter(this->corProfilerInfo, nullptr, moduleId, functionToken);
                    ILRewriterWrapper ilRewriterWrapper(&rewriter);

                    // hr = rewriter.Initialize();
                    hr = rewriter.Import();
                    RETURN_IF_FAILED(hr);

                    if (ilRewriterWrapper.ReplaceMethodCalls(method_replacement.target_method_token, wrapper_method_ref))
                    {
                        hr = rewriter.Export();
                        RETURN_IF_FAILED(hr);
                    }
                    else
                    {
                        // method IL was not modified: expected method call not found, intergration definition might be wrong
                        // TODO: log this
                    }
                }
                else
                {
                    // no method ref token found for wrapper method, we can't do the replacement,
                    // this should never happen because we always try to add the method ref in ModuleLoadFinished()
                    // TODO: log this
                }
            }
        }
    }

    // method IL was not modified
    return S_OK;
}

bool CorProfiler::IsAttached() const
{
    return bIsAttached;
}
