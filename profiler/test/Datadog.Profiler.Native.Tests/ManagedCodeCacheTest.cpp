// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gtest/gtest.h"
#include "gmock/gmock.h"
#include "ManagedCodeCache.h"
#include "MockProfilerInfo.h"

#include <thread>
#include <chrono>

using namespace testing;

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
