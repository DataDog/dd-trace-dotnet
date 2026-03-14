// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gtest/gtest.h"
#include "gmock/gmock.h"
#include "ManagedCodeCache.h"

#include <thread>
#include <chrono>

using namespace testing;

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

        if (riid == IID_ICorProfilerInfo4 || riid == IID_ICorProfilerInfo3 ||
            riid == IID_ICorProfilerInfo2 || riid == IID_ICorProfilerInfo ||
            riid == IID_IUnknown) {
            *ppvObject = this;
            AddRef();
            return S_OK;
        }

        *ppvObject = nullptr;
        return E_NOINTERFACE;
    }
    STDMETHOD_(ULONG, AddRef)() override { return ++_refCount; }
    STDMETHOD_(ULONG, Release)() override {
        ULONG count = --_refCount;
        if (count == 0) delete this;
        return count;
    }

    // Mocked methods (3 total - only what ManagedCodeCache uses)
    MOCK_METHOD(HRESULT, GetFunctionFromIP,
        (LPCBYTE ip, FunctionID* pFunctionId), (override));

    MOCK_METHOD(HRESULT, GetCodeInfo2,
        (FunctionID functionId, ULONG32 cCodeInfos, ULONG32* pcCodeInfos,
         COR_PRF_CODE_INFO codeInfos[]), (override));

    MOCK_METHOD(HRESULT, GetModuleInfo2,
        (ModuleID moduleId, LPCBYTE* ppBaseLoadAddress, ULONG cchName,
         ULONG* pcchName, WCHAR szName[], AssemblyID* pAssemblyId,
         DWORD* pdwModuleFlags), (override));

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

// Test fixture
class ManagedCodeCacheTest : public Test {
protected:
    MockProfilerInfo* mockProfiler;
    std::unique_ptr<ManagedCodeCache> cache;

    void SetUp() override {
        mockProfiler = new MockProfilerInfo();
        cache = std::make_unique<ManagedCodeCache>(mockProfiler);
        cache->Initialize();
    }

    void TearDown() override {
        cache.reset();
        mockProfiler->Release();
        mockProfiler = nullptr;
    }

    // Helper: Setup mock for AddFunction scenario
    void SetupMockCodeInfo(FunctionID funcId, uintptr_t start, ULONG32 size) {
        EXPECT_CALL(*mockProfiler, GetCodeInfo2(funcId, _, _, _))
            .WillOnce([start, size](FunctionID, ULONG32, ULONG32* pcCodeInfos,
                                   COR_PRF_CODE_INFO codeInfos[]) {
                if (pcCodeInfos != nullptr) {
                    *pcCodeInfos = 1;
                }
                if (codeInfos != nullptr) {
                    codeInfos[0].startAddress = start;
                    codeInfos[0].size = size;
                }
                return S_OK;
            });
    }

    // Helper: Wait for async worker thread
    void WaitForWorkerThread(int milliseconds = 100) {
        std::this_thread::sleep_for(std::chrono::milliseconds(milliseconds));
    }
};

// Test: Initialization succeeds
TEST_F(ManagedCodeCacheTest, Initialize_Succeeds) {
    ASSERT_NE(nullptr, cache);
}

// Test: Single code range
TEST_F(ManagedCodeCacheTest, AddFunction_SingleRange_GetFunctionIdReturnsCorrect) {
    FunctionID testFuncId = 12345;
    uintptr_t codeStart = 0x1000;
    ULONG32 codeSize = 0x200;

    SetupMockCodeInfo(testFuncId, codeStart, codeSize);

    cache->AddFunction(testFuncId);
    WaitForWorkerThread();

    // Test IPs within range
    EXPECT_EQ(testFuncId, cache->GetFunctionId(codeStart).value_or(0));
    EXPECT_EQ(testFuncId, cache->GetFunctionId(codeStart + 0x100).value_or(0));
    EXPECT_EQ(testFuncId, cache->GetFunctionId(codeStart + codeSize - 1).value_or(0));

    // IPs outside range should return nullopt
    EXPECT_FALSE(cache->GetFunctionId(codeStart - 1).has_value());
    EXPECT_FALSE(cache->GetFunctionId(codeStart + codeSize).has_value());
}

// Test: Multiple ranges (tiered JIT simulation)
TEST_F(ManagedCodeCacheTest, AddFunction_MultipleRanges_AccumulatesCorrectly) {
    FunctionID testFuncId = 67890;
    uintptr_t tier0Start = 0x2000;
    ULONG32 tier0Size = 0x100;
    uintptr_t tier1Start = 0x3000;
    ULONG32 tier1Size = 0x200;

    // First JIT (Tier 0)
    SetupMockCodeInfo(testFuncId, tier0Start, tier0Size);
    cache->AddFunction(testFuncId);
    WaitForWorkerThread();

    // Verify Tier 0 works
    EXPECT_EQ(testFuncId, cache->GetFunctionId(tier0Start + 0x50).value_or(0));

    // Second JIT (Tier 1)
    SetupMockCodeInfo(testFuncId, tier1Start, tier1Size);
    cache->AddFunction(testFuncId);
    WaitForWorkerThread();

    // Both ranges should work (accumulation)
    EXPECT_EQ(testFuncId, cache->GetFunctionId(tier0Start + 0x50).value_or(0));
    EXPECT_EQ(testFuncId, cache->GetFunctionId(tier1Start + 0x100).value_or(0));
}

// Test: IsManaged for valid managed IP
TEST_F(ManagedCodeCacheTest, IsManaged_ValidManagedIP_ReturnsTrue) {
    FunctionID testFuncId = 11111;
    uintptr_t codeStart = 0x4000;
    ULONG32 codeSize = 0x150;

    SetupMockCodeInfo(testFuncId, codeStart, codeSize);
    cache->AddFunction(testFuncId);
    WaitForWorkerThread();

    EXPECT_TRUE(cache->IsManaged(codeStart + 0x50));
}

// Test: IsManaged for invalid IP
TEST_F(ManagedCodeCacheTest, IsManaged_InvalidIP_ReturnsFalse) {
    EXPECT_FALSE(cache->IsManaged(0xDEADBEEF));
    EXPECT_FALSE(cache->IsManaged(0));
}

// Test: Multiple functions don't interfere
TEST_F(ManagedCodeCacheTest, AddFunction_MultipleFunctions_NoInterference) {
    FunctionID func1 = 100;
    FunctionID func2 = 200;
    uintptr_t code1Start = 0x5000;
    uintptr_t code2Start = 0x6000;
    ULONG32 codeSize = 0x100;

    SetupMockCodeInfo(func1, code1Start, codeSize);
    SetupMockCodeInfo(func2, code2Start, codeSize);

    cache->AddFunction(func1);
    cache->AddFunction(func2);
    WaitForWorkerThread();

    EXPECT_EQ(func1, cache->GetFunctionId(code1Start + 0x50).value_or(0));
    EXPECT_EQ(func2, cache->GetFunctionId(code2Start + 0x50).value_or(0));

    // No cross-contamination
    EXPECT_FALSE(cache->GetFunctionId(code1Start + codeSize + 10).has_value());
}

// Test: Thread safety (concurrent AddFunction calls)
TEST_F(ManagedCodeCacheTest, AddFunction_ConcurrentCalls_ThreadSafe) {
    const int numThreads = 4;
    const int functionsPerThread = 10;

    // Pre-setup all mock expectations (GoogleMock is not thread-safe for EXPECT_CALL)
    for (int t = 0; t < numThreads; t++) {
        for (int i = 0; i < functionsPerThread; i++) {
            FunctionID funcId = (t * 1000) + i;
            uintptr_t codeStart = 0x10000 + (funcId * 0x1000);
            ULONG32 codeSize = 0x100;

            SetupMockCodeInfo(funcId, codeStart, codeSize);
        }
    }

    // Now spawn threads to call AddFunction concurrently
    std::vector<std::thread> threads;
    for (int t = 0; t < numThreads; t++) {
        threads.emplace_back([this, t, functionsPerThread]() {
            for (int i = 0; i < functionsPerThread; i++) {
                FunctionID funcId = (t * 1000) + i;
                cache->AddFunction(funcId);
            }
        });
    }

    for (auto& thread : threads) {
        thread.join();
    }

    WaitForWorkerThread(500);  // Wait longer for all async operations

    // Verify no crashes and cache is still functional
    EXPECT_NE(nullptr, cache);
}

// Test: Signal safety of IsManaged (no blocking)
TEST_F(ManagedCodeCacheTest, IsManaged_ConcurrentAccess_IsSignalSafe) {
    FunctionID testFuncId = 999;
    uintptr_t codeStart = 0x7000;
    ULONG32 codeSize = 0x200;

    SetupMockCodeInfo(testFuncId, codeStart, codeSize);
    cache->AddFunction(testFuncId);
    WaitForWorkerThread();

    // Concurrent IsManaged calls (simulating signal handler scenario)
    const int numCalls = 1000;
    std::atomic<int> successCount{0};

    std::vector<std::thread> threads;
    for (int t = 0; t < 4; t++) {
        threads.emplace_back([this, codeStart, &successCount, numCalls]() {
            for (int i = 0; i < numCalls; i++) {
                if (cache->IsManaged(codeStart + 0x50)) {
                    successCount++;
                }
            }
        });
    }

    for (auto& thread : threads) {
        thread.join();
    }

    // All calls should succeed (no deadlocks)
    EXPECT_EQ(numCalls * 4, successCount.load());
}

// Test: Boundary conditions
TEST_F(ManagedCodeCacheTest, GetFunctionId_BoundaryIPs_CorrectBehavior) {
    FunctionID testFuncId = 555;
    uintptr_t codeStart = 0x8000;
    ULONG32 codeSize = 0x100;

    SetupMockCodeInfo(testFuncId, codeStart, codeSize);
    cache->AddFunction(testFuncId);
    WaitForWorkerThread();

    // Exact boundaries
    EXPECT_EQ(testFuncId, cache->GetFunctionId(codeStart).value_or(0));  // First byte (inclusive)
    EXPECT_EQ(testFuncId, cache->GetFunctionId(codeStart + codeSize - 1).value_or(0));  // Last byte (inclusive)

    // Just outside boundaries
    EXPECT_FALSE(cache->GetFunctionId(codeStart - 1).has_value());
    EXPECT_FALSE(cache->GetFunctionId(codeStart + codeSize).has_value());
}

// Test: Zero-sized code range
TEST_F(ManagedCodeCacheTest, AddFunction_ZeroSizeRange_HandledGracefully) {
    FunctionID testFuncId = 777;
    uintptr_t codeStart = 0x9000;
    ULONG32 codeSize = 0;

    SetupMockCodeInfo(testFuncId, codeStart, codeSize);
    cache->AddFunction(testFuncId);
    WaitForWorkerThread();

    // Should not crash, but may not find the function
    auto result = cache->GetFunctionId(codeStart);
    // Result is implementation-dependent, just verify no crash
    (void)result;
}

// Test: Very large code range
TEST_F(ManagedCodeCacheTest, AddFunction_LargeCodeRange_WorksCorrectly) {
    FunctionID testFuncId = 888;
    uintptr_t codeStart = 0x100000;
    ULONG32 codeSize = 0x10000;  // 64KB

    SetupMockCodeInfo(testFuncId, codeStart, codeSize);
    cache->AddFunction(testFuncId);
    WaitForWorkerThread();

    // Test various points in large range
    EXPECT_EQ(testFuncId, cache->GetFunctionId(codeStart).value_or(0));
    EXPECT_EQ(testFuncId, cache->GetFunctionId(codeStart + 0x8000).value_or(0));  // Middle
    EXPECT_EQ(testFuncId, cache->GetFunctionId(codeStart + codeSize - 1).value_or(0));  // End
}

// Test: Null IP
TEST_F(ManagedCodeCacheTest, GetFunctionId_NullIP_ReturnsEmpty) {
    EXPECT_FALSE(cache->GetFunctionId(0).has_value());
}

// Test: GetCodeInfo2 failure handling
TEST_F(ManagedCodeCacheTest, AddFunction_GetCodeInfo2Fails_HandledGracefully) {
    FunctionID testFuncId = 999;

    EXPECT_CALL(*mockProfiler, GetCodeInfo2(testFuncId, _, _, _))
        .WillOnce(Return(E_FAIL));

    cache->AddFunction(testFuncId);
    WaitForWorkerThread();

    // Should not crash
    EXPECT_FALSE(cache->GetFunctionId(0x1000).has_value());
}
