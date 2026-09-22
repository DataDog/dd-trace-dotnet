// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "gmock/gmock.h"

#include "cor.h"
#include "corprof.h"

#include <atomic>

// Mock ICorProfilerInfo4 with only 3 methods that ManagedCodeCache actually uses
class MockProfilerInfo : public ICorProfilerInfo4 {
public:
    virtual ~MockProfilerInfo() = default;

    // IUnknown
    STDMETHOD(QueryInterface)(REFIID riid, void** ppvObject) override {
        if (ppvObject == nullptr)
        {
            return E_POINTER;
        }

        *ppvObject = static_cast<ICorProfilerInfo4*>(this);
        AddRef();
        return S_OK;
    }
    STDMETHOD_(ULONG, AddRef)() override { return ++_refCount; }
    STDMETHOD_(ULONG, Release)() override {
        ULONG count = --_refCount;
        if (count == 0) delete this;
        return count;
    }

    // Mocked methods (3 total - only what ManagedCodeCache uses)
    MOCK_METHOD(HRESULT, GetFunctionFromIP,
        (LPCBYTE ip, FunctionID* pFunctionId),
        (override, Calltype(STDMETHODCALLTYPE)));

    MOCK_METHOD(HRESULT, GetCodeInfo2,
        (FunctionID functionId, ULONG32 cCodeInfos, ULONG32* pcCodeInfos,
         COR_PRF_CODE_INFO codeInfos[]),
        (override, Calltype(STDMETHODCALLTYPE)));

    MOCK_METHOD(HRESULT, GetModuleInfo2,
        (ModuleID moduleId, LPCBYTE* ppBaseLoadAddress, ULONG cchName,
         ULONG* pcchName, WCHAR szName[], AssemblyID* pAssemblyId,
         DWORD* pdwModuleFlags),
        (override, Calltype(STDMETHODCALLTYPE)));

    // All other ICorProfilerInfo4 methods - stub with E_NOTIMPL
    STDMETHOD(GetClassIDInfo)(ClassID classId, ModuleID* pModuleId, mdTypeDef* pTypeDefToken) override { return E_NOTIMPL; }
    STDMETHOD(GetFunctionInfo)(FunctionID functionId, ClassID* pClassId, ModuleID* pModuleId, mdToken* pToken) override { return E_NOTIMPL; }
    STDMETHOD(SetEventMask)(DWORD dwEvents) override { return E_NOTIMPL; }
    STDMETHOD(SetEnterLeaveFunctionHooks)(FunctionEnter* pFuncEnter, FunctionLeave* pFuncLeave, FunctionTailcall* pFuncTailcall) override { return E_NOTIMPL; }
    STDMETHOD(SetFunctionIDMapper)(FunctionIDMapper* pFunc) override { return E_NOTIMPL; }
    STDMETHOD(GetTokenAndMetaDataFromFunction)(FunctionID functionId, REFIID riid, IUnknown** ppImport, mdToken* pToken) override { return E_NOTIMPL; }
    STDMETHOD(GetModuleInfo)(ModuleID moduleId, LPCBYTE* ppBaseLoadAddress, ULONG cchName, ULONG* pcchName, WCHAR szName[], AssemblyID* pAssemblyId) override { return E_NOTIMPL; }
    STDMETHOD(GetModuleMetaData)(ModuleID moduleId, DWORD dwOpenFlags, REFIID riid, IUnknown** ppOut) override { return E_NOTIMPL; }
    STDMETHOD(GetILFunctionBody)(ModuleID moduleId, mdMethodDef methodId, LPCBYTE* ppMethodHeader, ULONG* pcbMethodSize) override { return E_NOTIMPL; }
    STDMETHOD(GetILFunctionBodyAllocator)(ModuleID moduleId, IMethodMalloc** ppMalloc) override { return E_NOTIMPL; }
    STDMETHOD(SetILFunctionBody)(ModuleID moduleId, mdMethodDef methodid, LPCBYTE pbNewILMethodHeader) override { return E_NOTIMPL; }
    STDMETHOD(GetAppDomainInfo)(AppDomainID appDomainId, ULONG cchName, ULONG* pcchName, WCHAR szName[], ProcessID* pProcessId) override { return E_NOTIMPL; }
    STDMETHOD(GetAssemblyInfo)(AssemblyID assemblyId, ULONG cchName, ULONG* pcchName, WCHAR szName[], AppDomainID* pAppDomainId, ModuleID* pModuleId) override { return E_NOTIMPL; }
    STDMETHOD(SetFunctionReJIT)(FunctionID functionId) override { return E_NOTIMPL; }
    STDMETHOD(ForceGC)() override { return E_NOTIMPL; }
    STDMETHOD(SetILInstrumentedCodeMap)(FunctionID functionId, BOOL fStartJit, ULONG cILMapEntries, COR_IL_MAP rgILMapEntries[]) override { return E_NOTIMPL; }
    STDMETHOD(GetInprocInspectionInterface)(IUnknown** ppicd) override { return E_NOTIMPL; }
    STDMETHOD(GetInprocInspectionIThisThread)(IUnknown** ppicd) override { return E_NOTIMPL; }
    STDMETHOD(GetThreadContext)(ThreadID threadId, ContextID* pContextId) override { return E_NOTIMPL; }
    STDMETHOD(BeginInprocDebugging)(BOOL fThisThreadOnly, DWORD* pdwProfilerContext) override { return E_NOTIMPL; }
    STDMETHOD(EndInprocDebugging)(DWORD dwProfilerContext) override { return E_NOTIMPL; }
    STDMETHOD(GetILToNativeMapping)(FunctionID functionId, ULONG32 cMap, ULONG32* pcMap, COR_DEBUG_IL_TO_NATIVE_MAP map[]) override { return E_NOTIMPL; }

    // ICorProfilerInfo2
    STDMETHOD(DoStackSnapshot)(ThreadID thread, StackSnapshotCallback* callback, ULONG32 infoFlags, void* clientData, BYTE* context, ULONG32 contextSize) override { return E_NOTIMPL; }
    STDMETHOD(SetEnterLeaveFunctionHooks2)(FunctionEnter2* pFuncEnter, FunctionLeave2* pFuncLeave, FunctionTailcall2* pFuncTailcall) override { return E_NOTIMPL; }
    STDMETHOD(GetFunctionInfo2)(FunctionID funcId, COR_PRF_FRAME_INFO frameInfo, ClassID* pClassId, ModuleID* pModuleId, mdToken* pToken, ULONG32 cTypeArgs, ULONG32* pcTypeArgs, ClassID typeArgs[]) override { return E_NOTIMPL; }
    STDMETHOD(GetStringLayout)(ULONG* pBufferLengthOffset, ULONG* pStringLengthOffset, ULONG* pBufferOffset) override { return E_NOTIMPL; }
    STDMETHOD(GetClassLayout)(ClassID classID, COR_FIELD_OFFSET rFieldOffset[], ULONG cFieldOffset, ULONG* pcFieldOffset, ULONG* pulClassSize) override { return E_NOTIMPL; }
    STDMETHOD(GetClassIDInfo2)(ClassID classId, ModuleID* pModuleId, mdTypeDef* pTypeDefToken, ClassID* pParentClassId, ULONG32 cNumTypeArgs, ULONG32* pcNumTypeArgs, ClassID typeArgs[]) override { return E_NOTIMPL; }
    STDMETHOD(GetCodeInfo)(FunctionID functionId, LPCBYTE* pStart, ULONG* pcSize) override { return E_NOTIMPL; }
    STDMETHOD(GetEventMask)(DWORD* pdwEvents) override { return E_NOTIMPL; }
    STDMETHOD(GetHandleFromThread)(ThreadID threadId, HANDLE* phThread) override { return E_NOTIMPL; }
    STDMETHOD(GetObjectSize)(ObjectID objectId, ULONG* pcSize) override { return E_NOTIMPL; }
    STDMETHOD(IsArrayClass)(ClassID classId, CorElementType* pBaseElemType, ClassID* pBaseClassId, ULONG* pcRank) override { return E_NOTIMPL; }
    STDMETHOD(GetThreadInfo)(ThreadID threadId, DWORD* pdwWin32ThreadId) override { return E_NOTIMPL; }
    STDMETHOD(GetCurrentThreadID)(ThreadID* pThreadId) override { return E_NOTIMPL; }
    STDMETHOD(GetClassFromObject)(ObjectID objectId, ClassID* pClassId) override { return E_NOTIMPL; }
    STDMETHOD(GetClassFromToken)(ModuleID moduleId, mdTypeDef typeDef, ClassID* pClassId) override { return E_NOTIMPL; }
    STDMETHOD(GetFunctionFromToken)(ModuleID moduleId, mdToken token, FunctionID* pFunctionId) override { return E_NOTIMPL; }
    STDMETHOD(EnumModuleFrozenObjects)(ModuleID moduleId, ICorProfilerObjectEnum** ppEnum) override { return E_NOTIMPL; }
    STDMETHOD(GetArrayObjectInfo)(ObjectID objectId, ULONG32 cDimensions, ULONG32 pDimensionSizes[], int pDimensionLowerBounds[], BYTE** ppData) override { return E_NOTIMPL; }
    STDMETHOD(GetBoxClassLayout)(ClassID classId, ULONG32* pBufferOffset) override { return E_NOTIMPL; }
    STDMETHOD(GetThreadAppDomain)(ThreadID threadId, AppDomainID* pAppDomainId) override { return E_NOTIMPL; }
    STDMETHOD(GetRVAStaticAddress)(ClassID classId, mdFieldDef fieldToken, void** ppAddress) override { return E_NOTIMPL; }
    STDMETHOD(GetAppDomainStaticAddress)(ClassID classId, mdFieldDef fieldToken, AppDomainID appDomainId, void** ppAddress) override { return E_NOTIMPL; }
    STDMETHOD(GetThreadStaticAddress)(ClassID classId, mdFieldDef fieldToken, ThreadID threadId, void** ppAddress) override { return E_NOTIMPL; }
    STDMETHOD(GetContextStaticAddress)(ClassID classId, mdFieldDef fieldToken, ContextID contextId, void** ppAddress) override { return E_NOTIMPL; }
    STDMETHOD(GetStaticFieldInfo)(ClassID classId, mdFieldDef fieldToken, COR_PRF_STATIC_TYPE* pFieldInfo) override { return E_NOTIMPL; }
    STDMETHOD(GetGenerationBounds)(ULONG cObjectRanges, ULONG* pcObjectRanges, COR_PRF_GC_GENERATION_RANGE ranges[]) override { return E_NOTIMPL; }
    STDMETHOD(GetObjectGeneration)(ObjectID objectId, COR_PRF_GC_GENERATION_RANGE* range) override { return E_NOTIMPL; }
    STDMETHOD(GetNotifiedExceptionClauseInfo)(COR_PRF_EX_CLAUSE_INFO* pinfo) override { return E_NOTIMPL; }

    // ICorProfilerInfo3
    STDMETHOD(EnumJITedFunctions)(ICorProfilerFunctionEnum** ppEnum) override { return E_NOTIMPL; }
    STDMETHOD(RequestProfilerDetach)(DWORD dwExpectedCompletionMilliseconds) override { return E_NOTIMPL; }
    STDMETHOD(SetFunctionIDMapper2)(FunctionIDMapper2* pFunc, void* clientData) override { return E_NOTIMPL; }
    STDMETHOD(GetStringLayout2)(ULONG* pStringLengthOffset, ULONG* pBufferOffset) override { return E_NOTIMPL; }
    STDMETHOD(SetEnterLeaveFunctionHooks3)(FunctionEnter3* pFuncEnter3, FunctionLeave3* pFuncLeave3, FunctionTailcall3* pFuncTailcall3) override { return E_NOTIMPL; }
    STDMETHOD(SetEnterLeaveFunctionHooks3WithInfo)(FunctionEnter3WithInfo* pFuncEnter3WithInfo, FunctionLeave3WithInfo* pFuncLeave3WithInfo, FunctionTailcall3WithInfo* pFuncTailcall3WithInfo) override { return E_NOTIMPL; }
    STDMETHOD(GetFunctionEnter3Info)(FunctionID functionId, COR_PRF_ELT_INFO eltInfo, COR_PRF_FRAME_INFO* pFrameInfo, ULONG* pcbArgumentInfo, COR_PRF_FUNCTION_ARGUMENT_INFO* pArgumentInfo) override { return E_NOTIMPL; }
    STDMETHOD(GetFunctionLeave3Info)(FunctionID functionId, COR_PRF_ELT_INFO eltInfo, COR_PRF_FRAME_INFO* pFrameInfo, COR_PRF_FUNCTION_ARGUMENT_RANGE* pRetvalRange) override { return E_NOTIMPL; }
    STDMETHOD(GetFunctionTailcall3Info)(FunctionID functionId, COR_PRF_ELT_INFO eltInfo, COR_PRF_FRAME_INFO* pFrameInfo) override { return E_NOTIMPL; }
    STDMETHOD(EnumModules)(ICorProfilerModuleEnum** ppEnum) override { return E_NOTIMPL; }
    STDMETHOD(GetRuntimeInformation)(USHORT* pClrInstanceId, COR_PRF_RUNTIME_TYPE* pRuntimeType, USHORT* pMajorVersion, USHORT* pMinorVersion, USHORT* pBuildNumber, USHORT* pQFEVersion, ULONG cchVersionString, ULONG* pcchVersionString, WCHAR szVersionString[]) override { return E_NOTIMPL; }
    STDMETHOD(GetThreadStaticAddress2)(ClassID classId, mdFieldDef fieldToken, AppDomainID appDomainId, ThreadID threadId, void** ppAddress) override { return E_NOTIMPL; }
    STDMETHOD(GetAppDomainsContainingModule)(ModuleID moduleId, ULONG32 cAppDomainIds, ULONG32* pcAppDomainIds, AppDomainID appDomainIds[]) override { return E_NOTIMPL; }
    STDMETHOD(GetClassFromTokenAndTypeArgs)(ModuleID moduleId, mdTypeDef typeDef, ULONG32 cTypeArgs, ClassID typeArgs[], ClassID* pClassId) override { return E_NOTIMPL; }
    STDMETHOD(GetFunctionFromTokenAndTypeArgs)(ModuleID moduleId, mdMethodDef funcDef, ClassID classId, ULONG32 cTypeArgs, ClassID typeArgs[], FunctionID* pFunctionId) override { return E_NOTIMPL; }

    // ICorProfilerInfo4
    STDMETHOD(EnumThreads)(ICorProfilerThreadEnum** ppEnum) override { return E_NOTIMPL; }
    STDMETHOD(InitializeCurrentThread)() override { return E_NOTIMPL; }
    STDMETHOD(RequestReJIT)(ULONG cFunctions, ModuleID moduleIds[], mdMethodDef methodIds[]) override { return E_NOTIMPL; }
    STDMETHOD(RequestRevert)(ULONG cFunctions, ModuleID moduleIds[], mdMethodDef methodIds[], HRESULT status[]) override { return E_NOTIMPL; }
    STDMETHOD(GetCodeInfo3)(FunctionID functionId, ReJITID reJitId, ULONG32 cCodeInfos, ULONG32* pcCodeInfos, COR_PRF_CODE_INFO codeInfos[]) override { return E_NOTIMPL; }
    STDMETHOD(GetFunctionFromIP2)(LPCBYTE ip, FunctionID* pFunctionId, ReJITID* pReJitId) override { return E_NOTIMPL; }
    STDMETHOD(GetReJITIDs)(FunctionID functionId, ULONG cReJitIds, ULONG* pcReJitIds, ReJITID reJitIds[]) override { return E_NOTIMPL; }
    STDMETHOD(GetILToNativeMapping2)(FunctionID functionId, ReJITID reJitId, ULONG32 cMap, ULONG32* pcMap, COR_DEBUG_IL_TO_NATIVE_MAP map[]) override { return E_NOTIMPL; }
    STDMETHOD(EnumJITedFunctions2)(ICorProfilerFunctionEnum** ppEnum) override { return E_NOTIMPL; }
    STDMETHOD(GetObjectSize2)(ObjectID objectId, SIZE_T* pcSize) override { return E_NOTIMPL; }

private:
    std::atomic<ULONG> _refCount{1};
};
