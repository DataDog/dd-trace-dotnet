// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#include <string>
#include <fstream>
#include <vector>
#include "CorProfiler.h"
#include "Macros.h"
#include "ComPtr.h"
#include "TypeReference.h"
#include "MemberReference.h"
#include "ModuleMetadata.h"
#include "ILRewriter.h"
#include "ILRewriterWrapper.h"
#include "MetadataBuilder.h"
#include "AspNetMvc5Integration.h"
#include "CustomIntegration.h"

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

    WCHAR wszProcessNames[MAX_PATH] = { L'\0' };
    WCHAR wszCurrentProcessPath[MAX_PATH] = { L'\0' };

    if (FAILED(GetEnvironmentVariable(L"DATADOG_PROFILE_PROCESSES", wszProcessNames, _countof(wszProcessNames))))
    {
        LOG_APPEND(L"Failed to attach profiler: could not get DATADOG_PROFILE_PROCESSES environment variable.");
        return E_FAIL;
    }

    LOG_APPEND(L"DATADOG_PROFILE_PROCESSES = " << wszProcessNames);

    if (FAILED(GetModuleFileName(NULL, wszCurrentProcessPath, _countof(wszCurrentProcessPath))))
    {
        LOG_APPEND(L"Failed to attach profiler: could not get current module filename.");
        return E_FAIL;
    }

    LOG_APPEND(L"Module file name = " << wszCurrentProcessPath);

    WCHAR* lastSeparator = wcsrchr(wszCurrentProcessPath, L'\\');
    WCHAR* processName = lastSeparator == nullptr ? wszCurrentProcessPath : lastSeparator + 1;

    if (wcsstr(wszProcessNames, processName) == nullptr)
    {
        LOG_APPEND(L"Profiler disabled: module name \"" << processName << "\" does not match DATADOG_PROFILE_PROCESSES environment variable.");
        return E_FAIL;
    }

    if (FAILED(pICorProfilerInfoUnk->QueryInterface(__uuidof(ICorProfilerInfo3), reinterpret_cast<void **>(&this->corProfilerInfo))))
    {
        // we need at least ICorProfilerInfo3 to call GetModuleInfo2()
        LOG_APPEND(L"Profiler disabled: interface ICorProfilerInfo3 or higher not found.");
        return E_FAIL;
    }

    const DWORD eventMask = COR_PRF_MONITOR_JIT_COMPILATION |
                            COR_PRF_DISABLE_TRANSPARENCY_CHECKS_UNDER_FULL_TRUST | /* helps the case where this profiler is used on Full CLR */
                            // COR_PRF_DISABLE_INLINING |
                            COR_PRF_MONITOR_MODULE_LOADS |
                            //COR_PRF_MONITOR_ASSEMBLY_LOADS |
                            //COR_PRF_MONITOR_APPDOMAIN_LOADS |
                            // COR_PRF_ENABLE_REJIT |
                            COR_PRF_DISABLE_ALL_NGEN_IMAGES;

    if (FAILED(this->corProfilerInfo->SetEventMask(eventMask)))
    {
        LOG_APPEND(L"Failed to attach profiler: unable to set event mask.");
        return E_FAIL;
    }

    // we're in!
    LOG_APPEND(L"Profiler attached to " << processName);
    bIsAttached = true;
    g_pCallbackObject = this;
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfiler::ModuleLoadFinished(ModuleID moduleId, HRESULT hrStatus)
{
    LPCBYTE pbBaseLoadAddr;
    WCHAR wszModulePath[300];
    ULONG cchNameIn = _countof(wszModulePath);
    ULONG cchNameOut;
    AssemblyID assemblyId;
    DWORD dwModuleFlags;

    HRESULT hr = this->corProfilerInfo->GetModuleInfo2(moduleId,
                                                       &pbBaseLoadAddr,
                                                       cchNameIn,
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

    WCHAR assemblyName[512];
    ULONG assemblyNameLength;
    hr = this->corProfilerInfo->GetAssemblyInfo(assemblyId, _countof(assemblyName), &assemblyNameLength, assemblyName, nullptr, nullptr);
    LOG_IFFAILEDRET(hr, L"Failed to get assembly name.");

    std::vector<IntegrationBase> allIntegrations = {
        &AspNetMvc5Integration,
        &CustomIntegration,
    };

    std::vector<IntegrationBase> enabledIntegrations;

    // find enabled integrations that need to instrument methods in this module
    for (const IntegrationBase& integration : allIntegrations)
    {
        if (integration.IsEnabled())
        {
            for (const MemberReference& instrumentedMethod : integration.GetInstrumentedMethods())
            {
                if (instrumentedMethod.ContainingType.AssemblyName == assemblyName)
                {
                    enabledIntegrations.push_back(integration);
                    break;
                }
            }
        }
    }

    if (enabledIntegrations.empty())
    {
        // we don't need to instrument anything in this module
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

    ModuleMetadata moduleMetadata(metadataImport,
                                  assemblyName,
                                  enabledIntegrations);

    MetadataBuilder metadataBuilder(moduleMetadata,
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
    hr = metadataBuilder.EmitAssemblyRef(L"Datadog.Trace.ClrProfiler.Managed",
                                         assemblyMetaData,
                                         rgbPublicKeyToken,
                                         _countof(rgbPublicKeyToken),
                                         assemblyRef);

    RETURN_IF_FAILED(hr);

    static const std::vector<MemberReference> instrumentationProbes = {
        Datadog_Trace_ClrProfiler_Instrumentation_OnMethodEntered,
        Datadog_Trace_ClrProfiler_Instrumentation_OnMethodExit_ReturnVoid,
        Datadog_Trace_ClrProfiler_Instrumentation_OnMethodExit_ReturnObject
    };

    // for each instrumentation probe...
    for (const MemberReference& instrumentationProbe : instrumentationProbes)
    {
        // find or create any typeRefs and memberRefs needed
        hr = metadataBuilder.ResolveMember(instrumentationProbe);
        RETURN_IF_FAILED(hr);
    }

    // for each enabled integration's instrumented method...
    for (const IntegrationBase& integration : enabledIntegrations)
    {
        for (const MemberReference& instrumentedMethod : integration.GetInstrumentedMethods())
        {
            // find or create any typeRefs and memberRefs needed
            hr = metadataBuilder.ResolveMember(instrumentedMethod);
            RETURN_IF_FAILED(hr);
        }
    }

    // store module info for later lookup
    m_moduleIDToInfoMap.Update(moduleId, moduleMetadata);
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfiler::ModuleUnloadFinished(ModuleID moduleId, HRESULT hrStatus)
{
    // TODO: release COM pointers
    m_moduleIDToInfoMap.Erase(moduleId);
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfiler::JITCompilationStarted(FunctionID functionId, BOOL fIsSafeToBlock)
{
    ClassID classId;
    ModuleID moduleId;
    mdToken functiontoken;

    HRESULT hr = this->corProfilerInfo->GetFunctionInfo(functionId, &classId, &moduleId, &functiontoken);
    RETURN_IF_FAILED(hr);

    ModuleMetadata moduleMetadata{};

    if (!m_moduleIDToInfoMap.LookupIfExists(moduleId, &moduleMetadata))
    {
        // we haven't stored a ModuleInfo for this module, so we can't modify its IL
        return S_OK;
    }

    WCHAR wszTypeDefName[256] = {};
    WCHAR wszMethodDefName[256] = {};

    moduleMetadata.GetClassAndFunctionNamesFromMethodDef(functiontoken,
                                                         wszTypeDefName,
                                                         _countof(wszTypeDefName),
                                                         wszMethodDefName,
                                                         _countof(wszMethodDefName));

    const auto typeName = std::wstring(wszTypeDefName);
    const auto methodName = std::wstring(wszMethodDefName);

    // check if this method should be instrumented
    // NOTE: for now we only allow one integration to instrument a method,
    // so first integration wins
    for (const IntegrationBase& integration : moduleMetadata.m_Integrations)
    {
        for (const MemberReference& instrumentedMethod : integration.GetInstrumentedMethods())
        {
            // TODO: match by complete signature, not just name
            if (typeName == instrumentedMethod.ContainingType.TypeName && methodName == instrumentedMethod.MethodName)
            {
                const MemberReference& exitProbe = instrumentedMethod.ReturnType == GlobalTypeReferences.System_Void
                                                       ? Datadog_Trace_ClrProfiler_Instrumentation_OnMethodExit_ReturnVoid
                                                       : Datadog_Trace_ClrProfiler_Instrumentation_OnMethodExit_ReturnObject;

                hr = RewriteIL(this->corProfilerInfo,
                               nullptr,
                               integration,
                               instrumentedMethod,
                               moduleId,
                               functiontoken,
                               moduleMetadata,
                               Datadog_Trace_ClrProfiler_Instrumentation_OnMethodEntered,
                               exitProbe);

                if (SUCCEEDED(hr))
                {
                    LOG_APPEND("Finished rewriting IL for " << wszTypeDefName << "." << wszMethodDefName << "()");
                    return S_OK;
                }

                LOG_APPEND("Failed rewriting IL for " << wszTypeDefName << "." << wszMethodDefName << "(). HR = " << HEX(hr));
                return hr;
            }
        }
    }

    // method IL was not modified
    return S_OK;
}

// Uses the general-purpose ILRewriter class to import original
// IL, rewrite it, and send the result to the CLR
HRESULT CorProfiler::RewriteIL(ICorProfilerInfo* const pICorProfilerInfo,
                               ICorProfilerFunctionControl* const pICorProfilerFunctionControl,
                               const IntegrationBase& integration,
                               const MemberReference& instrumentedMethod,
                               const ModuleID moduleID,
                               const mdToken functionToken,
                               const ModuleMetadata& moduleMetadata,
                               const MemberReference& entryProbe,
                               const MemberReference& exitProbe)
{
    ILRewriter rewriter(pICorProfilerInfo, pICorProfilerFunctionControl, moduleID, functionToken);
    ILRewriterWrapper ilRewriterWrapper(&rewriter, moduleMetadata);

    // hr = rewriter.Initialize();
    HRESULT hr = rewriter.Import();
    RETURN_IF_FAILED(hr);

    // insert a call to the entry probe before the first IL instruction
    ilRewriterWrapper.SetILPosition(rewriter.GetILList()->m_pNext);
    integration.InjectEntryProbe(ilRewriterWrapper, moduleID, functionToken, instrumentedMethod, entryProbe);

    // Find all RETs, and insert a call to the exit probe before each one
    for (ILInstr* pInstr = rewriter.GetILList()->m_pNext;
         pInstr != rewriter.GetILList();
         pInstr = pInstr->m_pNext)
    {
        if (pInstr->m_opcode == CEE_RET)
        {
            // We want any branches or leaves that targeted the RET instruction to
            // actually target the epilog instructions we're adding. So turn the "RET"
            // into ["NOP", "RET"], and THEN add the epilog between the NOP & RET. That
            // ensures that any branches that went to the RET will now go to the NOP and
            // then execute our epilog.

            // RET->NOP
            pInstr->m_opcode = CEE_NOP;

            // Add the new RET after
            ILInstr* pNewRet = rewriter.NewILInstr();
            pNewRet->m_opcode = CEE_RET;
            rewriter.InsertAfter(pInstr, pNewRet);

            // And now insert the epilog before the new RET
            ilRewriterWrapper.SetILPosition(pNewRet);
            integration.InjectExitProbe(ilRewriterWrapper, instrumentedMethod, exitProbe);

            // Advance pInstr after all this gunk so the for loop continues properly
            pInstr = pNewRet;
        }
    }

    hr = rewriter.Export();
    RETURN_IF_FAILED(hr);

    return S_OK;
}

bool CorProfiler::IsAttached() const
{
    return bIsAttached;
}

bool CorProfiler::GetMetadataNames(ModuleID moduleId,
                                   mdMethodDef methodToken,
                                   LPWSTR wszModulePath,
                                   ULONG cchModulePath,
                                   LPWSTR wszTypeDefName,
                                   ULONG cchTypeDefName,
                                   LPWSTR wszMethodDefName,
                                   ULONG cchMethodDefName)
{
    ModuleMetadata moduleMetadata{};

    if (m_moduleIDToInfoMap.LookupIfExists(moduleId, &moduleMetadata))
    {
        wcscpy_s(wszModulePath, cchModulePath, moduleMetadata.assemblyName.c_str());

        moduleMetadata.GetClassAndFunctionNamesFromMethodDef(methodToken,
                                                             wszTypeDefName,
                                                             cchTypeDefName,
                                                             wszMethodDefName,
                                                             cchMethodDefName);

        return true;
    }

    return false;
}
