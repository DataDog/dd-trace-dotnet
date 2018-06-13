// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#include <string>
#include <fstream>
#include <vector>
#include "CorProfiler.h"
#include "Macros.h"
#include "CComPtr.h"
#include "TypeReference.h"
#include "MemberReference.h"
#include "GlobalIntegrations.h"

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

    std::vector<const IntegrationBase*> enabledIntegrations;

    // find enabled integrations that need to instrument methods in this module
    for (const IntegrationBase* integration : GlobalIntegrations.All)
    {
        if (integration->IsEnabled())
        {
            for (const MemberReference& instrumentedMethod : integration->GetInstrumentedMethods())
            {
                // TODO: research module name vs assembly name, always the same in C#?
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

    // get metadata interfaces
    CComPtr<IMetaDataImport> metadataImport;
    hr = this->corProfilerInfo->GetModuleMetaData(moduleId,
                                                  ofRead | ofWrite,
                                                  IID_IMetaDataImport,
                                                  reinterpret_cast<IUnknown **>(&metadataImport));
    RETURN_IF_FAILED(hr);

    CComPtr<IMetaDataEmit> metadataEmit;
    hr = metadataImport->QueryInterface(IID_IMetaDataEmit, reinterpret_cast<void **>(&metadataEmit));
    RETURN_IF_FAILED(hr);

    CComPtr<IMetaDataAssemblyEmit> assemblyEmit;
    hr = metadataImport->QueryInterface(IID_IMetaDataAssemblyEmit, reinterpret_cast<void **>(&assemblyEmit));
    RETURN_IF_FAILED(hr);

    CComPtr<IMetaDataAssemblyImport> assemblyImport;
    hr = metadataImport->QueryInterface(IID_IMetaDataAssemblyImport, reinterpret_cast<void **>(&assemblyImport));
    RETURN_IF_FAILED(hr);

    ModuleInfo moduleInfo{};
    moduleInfo.m_pImport = metadataImport;
    moduleInfo.m_pImport->AddRef();
    moduleInfo.m_Integrations = enabledIntegrations;

    if (wcscpy_s(moduleInfo.m_wszModulePath, _countof(moduleInfo.m_wszModulePath), wszModulePath) != 0)
    {
        LOG_APPEND(L"Failed to store module path '" << wszModulePath << L"'");
    }

    mdModule module;
    hr = metadataImport->GetModuleFromScope(&module);
    LOG_IFFAILEDRET(hr, L"Failed to get module token.");

    // emit a metadata reference to our assembly, Datadog.Trace.ClrProfiler.Managed
    mdAssemblyRef assemblyRef;
    hr = EmitAssemblyRef(assemblyEmit, &assemblyRef);

    // find or create mdTypeRef tokens and save them for later
    for (const TypeReference& typeReference : GlobalTypeReferences.All)
    {
        hr = ResolveTypeReference(typeReference,
                                  assemblyName,
                                  metadataImport,
                                  metadataEmit,
                                  assemblyImport,
                                  module,
                                  moduleInfo.m_TypeRefLookup);
    }

    static const std::vector<MemberReference> instrumentationProbes = {
        Datadog_Trace_ClrProfiler_Instrumentation_OnMethodEntered,
        Datadog_Trace_ClrProfiler_Instrumentation_OnMethodExit_ReturnVoid,
        Datadog_Trace_ClrProfiler_Instrumentation_OnMethodExit_ReturnObject
    };

    // add the references to our helper methods
    for (const MemberReference& memberReference : instrumentationProbes)
    {
        hr = RevolveMemberReference(memberReference,
                                    metadataImport,
                                    metadataEmit,
                                    moduleInfo.m_TypeRefLookup,
                                    moduleInfo.m_MemberRefLookup);
    }

    // find or create references to types and methods needed by each enabled integration
    for (const IntegrationBase* const enabledIntegration : enabledIntegrations)
    {
        const std::vector<TypeReference>& typeReferences = enabledIntegration->GetTypeReferences();

        // find or create mdTypeRef tokens and save them for later
        for (const TypeReference& typeReference : typeReferences)
        {
            hr = ResolveTypeReference(typeReference,
                                      assemblyName,
                                      metadataImport,
                                      metadataEmit,
                                      assemblyImport,
                                      module,
                                      moduleInfo.m_TypeRefLookup);
        }

        const std::vector<MemberReference>& memberReferences = enabledIntegration->GetMemberReferences();

        // find or create mdMemberRef tokens and save them for later
        for (const MemberReference& memberReference : memberReferences)
        {
            hr = RevolveMemberReference(memberReference,
                                        metadataImport,
                                        metadataEmit,
                                        moduleInfo.m_TypeRefLookup,
                                        moduleInfo.m_MemberRefLookup);
        }
    }

    RETURN_IF_FAILED(hr);

    // store module info for later lookup
    m_moduleIDToInfoMap.Update(moduleId, moduleInfo);
    return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfiler::ModuleUnloadFinished(ModuleID moduleId, HRESULT hrStatus)
{
    // TODO: release COM pointers, low priority
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

    ModuleInfo moduleInfo{};

    if (!m_moduleIDToInfoMap.LookupIfExists(moduleId, &moduleInfo))
    {
        // we haven't stored a ModuleInfo for this module, so we can't modify its IL
        return S_OK;
    }

    WCHAR wszTypeDefName[256];
    WCHAR wszMethodDefName[256];

    GetClassAndFunctionNamesFromMethodDef(moduleInfo.m_pImport,
                                          functiontoken,
                                          wszTypeDefName,
                                          _countof(wszTypeDefName),
                                          wszMethodDefName,
                                          _countof(wszMethodDefName));

    const auto typeName = std::wstring(wszTypeDefName);
    const auto methodName = std::wstring(wszMethodDefName);

    // check if this method should be instrumented
    // NOTE: for now we only allow one integration to instrument a method,
    // so first integration wins
    for (const IntegrationBase* const integration : moduleInfo.m_Integrations)
    {
        for (const MemberReference& instrumentedMethod : integration->GetInstrumentedMethods())
        {
            if (typeName == instrumentedMethod.ContainingType.TypeName && methodName == instrumentedMethod.MethodName)
            {
                const MemberReference& exitProbe = instrumentedMethod.ReturnType == GlobalTypeReferences.System_Void
                                                       ? Datadog_Trace_ClrProfiler_Instrumentation_OnMethodExit_ReturnVoid
                                                       : Datadog_Trace_ClrProfiler_Instrumentation_OnMethodExit_ReturnObject;

                hr = RewriteIL(this->corProfilerInfo,
                               nullptr,
                               integration,
                               moduleId,
                               functiontoken,
                               moduleInfo.m_TypeRefLookup,
                               moduleInfo.m_MemberRefLookup,
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
                               const IntegrationBase* integration,
                               const ModuleID moduleID,
                               const mdToken functionToken,
                               const TypeRefLookup& typeDefLookup,
                               const MemberRefLookup& memberRefLookup,
                               const MemberReference& entryProbe,
                               const MemberReference& exitProbe)
{
    ILRewriter rewriter(pICorProfilerInfo, pICorProfilerFunctionControl, moduleID, functionToken);
    ILRewriterWrapper ilRewriterWrapper(&rewriter, typeDefLookup, memberRefLookup);

    // hr = rewriter.Initialize();
    HRESULT hr = rewriter.Import();
    RETURN_IF_FAILED(hr);

    // insert a call to the entry probe before the first IL instruction
    ilRewriterWrapper.SetILPosition(rewriter.GetILList()->m_pNext);
    integration->InjectEntryProbe(ilRewriterWrapper, moduleID, functionToken, entryProbe);

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
            integration->InjectExitProbe(ilRewriterWrapper, exitProbe);

            // Advance pInstr after all this gunk so the for loop continues properly
            pInstr = pNewRet;
        }
    }

    hr = rewriter.Export();
    RETURN_IF_FAILED(hr);

    return S_OK;
}

HRESULT CorProfiler::FindAssemblyRef(const std::wstring& assemblyName,
                                     IMetaDataAssemblyImport* pAssemblyImport,
                                     mdAssemblyRef* assemblyRef)
{
    HCORENUM hEnum = nullptr;
    mdAssemblyRef rgAssemblyRefs[20];
    ULONG cAssemblyRefsReturned;

    do
    {
        const HRESULT hr = pAssemblyImport->EnumAssemblyRefs(&hEnum,
                                                             rgAssemblyRefs,
                                                             _countof(rgAssemblyRefs),
                                                             &cAssemblyRefsReturned);

        LOG_IFFAILEDRET(hr, L"EnumAssemblyRefs failed, hr = " << HEX(hr));

        if (cAssemblyRefsReturned == 0)
        {
            pAssemblyImport->CloseEnum(hEnum);
            LOG_APPEND(L"Could not find an AssemblyRef to " << assemblyName);
            return E_FAIL;
        }
    }
    while (FindAssemblyRefIterator(assemblyName,
                                   pAssemblyImport,
                                   rgAssemblyRefs,
                                   cAssemblyRefsReturned,
                                   assemblyRef) < S_OK);

    pAssemblyImport->CloseEnum(hEnum);
    return S_OK;
}

HRESULT CorProfiler::FindAssemblyRefIterator(const std::wstring& assemblyName,
                                             IMetaDataAssemblyImport* pAssemblyImport,
                                             mdAssemblyRef* rgAssemblyRefs,
                                             ULONG cAssemblyRefs,
                                             mdAssemblyRef* assemblyRef)
{
    for (ULONG i = 0; i < cAssemblyRefs; i++)
    {
        const void* pvPublicKeyOrToken;
        ULONG cbPublicKeyOrToken;
        WCHAR wszName[512];
        ULONG cchNameReturned;
        ASSEMBLYMETADATA asmMetaData{};
        //ZeroMemory(&asmMetaData, sizeof(asmMetaData));
        const void* pbHashValue;
        ULONG cbHashValue;
        DWORD asmRefFlags;

        const HRESULT hr = pAssemblyImport->GetAssemblyRefProps(rgAssemblyRefs[i],
                                                                &pvPublicKeyOrToken,
                                                                &cbPublicKeyOrToken,
                                                                wszName,
                                                                _countof(wszName),
                                                                &cchNameReturned,
                                                                &asmMetaData,
                                                                &pbHashValue,
                                                                &cbHashValue,
                                                                &asmRefFlags);

        LOG_IFFAILEDRET(hr,L"GetAssemblyRefProps failed, hr = " << HEX(hr));

        if (assemblyName == wszName)
        {
            *assemblyRef = rgAssemblyRefs[i];
            return S_OK;
        }
    }

    return E_FAIL;
}

/**
 * \brief Generate assemblyRef for Datadog.Trace.ClrProfiler.Managed, Version=1.0.0.0, Culture=neutral, PublicKeyToken=def86d061d0d2eeb
 */
HRESULT CorProfiler::EmitAssemblyRef(IMetaDataAssemblyEmit* pAssemblyEmit, mdAssemblyRef* assemblyRef)
{
    BYTE rgbPublicKeyToken[] = { 0xde, 0xf8, 0x6d, 0x06, 0x1d, 0x0d, 0x2e, 0xeb };
    WCHAR wszLocale[] = L"neutral";

    ASSEMBLYMETADATA assemblyMetaData{};
    assemblyMetaData.usMajorVersion = 1;
    assemblyMetaData.usMinorVersion = 0;
    assemblyMetaData.usBuildNumber = 0;
    assemblyMetaData.usRevisionNumber = 0;
    assemblyMetaData.szLocale = wszLocale;
    assemblyMetaData.cbLocale = _countof(wszLocale);

    const HRESULT hr = pAssemblyEmit->DefineAssemblyRef(static_cast<void *>(rgbPublicKeyToken),
                                                        sizeof(rgbPublicKeyToken),
                                                        L"Datadog.Trace.ClrProfiler.Managed",
                                                        &assemblyMetaData,
                                                        // hash blob
                                                        nullptr,
                                                        // cb of hash blob
                                                        0,
                                                        // flags
                                                        0,
                                                        assemblyRef);

    LOG_IFFAILEDRET(hr, L"DefineAssemblyRef failed");
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
    ModuleInfo moduleInfo{};

    if (m_moduleIDToInfoMap.LookupIfExists(moduleId, &moduleInfo))
    {
        wcscpy_s(wszModulePath, cchModulePath, moduleInfo.m_wszModulePath);

        GetClassAndFunctionNamesFromMethodDef(moduleInfo.m_pImport,
                                              methodToken,
                                              wszTypeDefName,
                                              cchTypeDefName,
                                              wszMethodDefName,
                                              cchMethodDefName);

        return true;
    }

    return false;
}

// [private] Gets the text names from a method def.
void CorProfiler::GetClassAndFunctionNamesFromMethodDef(IMetaDataImport* pImport,
                                                        mdMethodDef methodDef,
                                                        LPWSTR wszTypeDefName,
                                                        ULONG cchTypeDefName,
                                                        LPWSTR wszMethodDefName,
                                                        ULONG cchMethodDefName)
{
    mdTypeDef typeDef;
    ULONG cchMethodDefActual;
    DWORD dwMethodAttr;
    ULONG cchTypeDefActual;
    DWORD dwTypeDefFlags;
    mdTypeDef typeDefBase;

    HRESULT hr = pImport->GetMethodProps(methodDef,
                                         &typeDef,
                                         wszMethodDefName,
                                         cchMethodDefName,
                                         &cchMethodDefActual,
                                         &dwMethodAttr,
                                         // [OUT] point to the blob value of meta data
                                         nullptr,
                                         // [OUT] actual size of signature blob
                                         nullptr,
                                         // [OUT] codeRVA
                                         nullptr,
                                         // [OUT] Impl. Flags
                                         nullptr);

    if (FAILED(hr))
    {
        LOG_APPEND(L"GetMethodProps failed for methodDef = " << HEX(methodDef) << L", hr = " << HEX(hr));
    }

    hr = pImport->GetTypeDefProps(typeDef,
                                  wszTypeDefName,
                                  cchTypeDefName,
                                  &cchTypeDefActual,
                                  &dwTypeDefFlags,
                                  &typeDefBase);

    if (FAILED(hr))
    {
        LOG_APPEND(L"GetTypeDefProps failed for typeDef = " << HEX(typeDef) << L", hr = " << HEX(hr));
    }
}

HRESULT CorProfiler::ResolveTypeReference(const TypeReference& type,
                                          const std::wstring& assemblyName,
                                          IMetaDataImport* metadataImport,
                                          IMetaDataEmit* metadataEmit,
                                          IMetaDataAssemblyImport* assemblyImport,
                                          const mdModule module,
                                          TypeRefLookup& typeRefLookup)
{
    HRESULT hr;

    if (assemblyName == type.AssemblyName)
    {
        // type is defined in this assembly
        mdTypeRef typeRef = mdTypeRefNil;
        hr = metadataEmit->DefineTypeRefByName(module, type.TypeName.c_str(), &typeRef);

        if (SUCCEEDED(hr))
        {
            typeRefLookup[type] = typeRef;
        }
    }
    else
    {
        // type is defined in another assembly,
        // find a reference to the assembly where type lives
        mdAssemblyRef assemblyRef = mdAssemblyRefNil;
        hr = FindAssemblyRef(type.AssemblyName, assemblyImport, &assemblyRef);

        // TODO: emit assembly reference if not found

        if (SUCCEEDED(hr))
        {
            // search for an existing reference to the type
            mdTypeRef typeRef = mdTypeRefNil;
            hr = metadataImport->FindTypeRef(assemblyRef, type.TypeName.c_str(), &typeRef);

            if (hr == HRESULT(0x80131130) /* record not found on lookup */)
            {
                // if typeRef not found, create a new one by emiting a metadata token
                hr = metadataEmit->DefineTypeRefByName(assemblyRef, type.TypeName.c_str(), &typeRef);
            }

            if (SUCCEEDED(hr))
            {
                typeRefLookup[type] = typeRef;
            }
        }
    }

    return S_OK;
}

HRESULT CorProfiler::RevolveMemberReference(const MemberReference& method,
                                            IMetaDataImport* metadataImport,
                                            IMetaDataEmit* metadataEmit,
                                            const TypeRefLookup& typeRefLookup,
                                            MemberRefLookup& memberRefLookup)
{
    const mdTypeRef typeRef = typeRefLookup[method.ContainingType];
    mdMemberRef memberRef = mdMemberRefNil;

    COR_SIGNATURE pSignature[128]{};
    const ULONG signatureLength = method.CreateSignature(typeRefLookup, pSignature);

    HRESULT hr = metadataImport->FindMemberRef(typeRef, method.MethodName.c_str(), pSignature, signatureLength, &memberRef);

    if (hr == HRESULT(0x80131130) /* record not found on lookup */)
    {
        // if memberRef not found, create it by emiting a metadata token
        hr = metadataEmit->DefineMemberRef(typeRef, method.MethodName.c_str(), pSignature, signatureLength, &memberRef);
    }

    if (SUCCEEDED(hr))
    {
        memberRefLookup[method] = memberRef;
    }

    return S_OK;
}
