// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gtest/gtest.h"
#include "gmock/gmock.h"
#include "ManagedCodeCache.h"
#include "MockProfilerInfo.h"

#include <atomic>
#include <chrono>
#include <thread>
#include <vector>

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

    // Shortcut to get only the function id from the returned FunctionInfo
    FunctionID GetFuncIdOr0(uintptr_t ip) {
        auto info = cache->GetFunctionInfo(ip);
        return info.has_value() ? info->FunctionId : 0;
    }
};

// Test: Single code range
TEST_F(ManagedCodeCacheTest, AddFunction_SingleRange_GetFunctionInfoReturnsCorrect) {
    FunctionID testFuncId = 12345;
    uintptr_t codeStart = 0x1000;
    ULONG32 codeSize = 0x200;

    SetupMockCodeInfo(testFuncId, codeStart, codeSize);

    cache->AddFunction(testFuncId, /*isDynamic*/ false);
    // Test IPs within range
    EXPECT_EQ(testFuncId, GetFuncIdOr0(codeStart));
    EXPECT_EQ(testFuncId, GetFuncIdOr0(codeStart + 0x100));
    EXPECT_EQ(testFuncId, GetFuncIdOr0(codeStart + codeSize - 1));

    // IPs outside range should return InvalidFunctionId (still "found", just not managed)
    auto beforeStartIp = cache->GetFunctionInfo(codeStart - 1);
    EXPECT_TRUE(beforeStartIp.has_value());
    EXPECT_EQ(ManagedCodeCache::InvalidFunctionId, beforeStartIp->FunctionId);
    auto borderIp = cache->GetFunctionInfo(codeStart + codeSize);
    EXPECT_TRUE(borderIp.has_value());
    EXPECT_EQ(ManagedCodeCache::InvalidFunctionId, borderIp->FunctionId);
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
    cache->AddFunction(testFuncId, /*isDynamic*/ false);
    // Verify Tier 0 works
    EXPECT_EQ(testFuncId, GetFuncIdOr0(tier0Start + 0x50));

    // Second JIT (Tier 1)
    SetupMockCodeInfo(testFuncId, tier1Start, tier1Size);
    cache->AddFunction(testFuncId, /*isDynamic*/ false);
    // Both ranges should work (accumulation)
    EXPECT_EQ(testFuncId, GetFuncIdOr0(tier0Start + 0x50));
    EXPECT_EQ(testFuncId, GetFuncIdOr0(tier1Start + 0x100));
}

// Test: IsManaged for valid managed IP
TEST_F(ManagedCodeCacheTest, IsManaged_ValidManagedIP_ReturnsTrue) {
    FunctionID testFuncId = 11111;
    uintptr_t codeStart = 0x4000;
    ULONG32 codeSize = 0x150;

    SetupMockCodeInfo(testFuncId, codeStart, codeSize);
    cache->AddFunction(testFuncId, /*isDynamic*/ false);
    EXPECT_TRUE(cache->IsManaged(codeStart + 0x50));
}

// Test: IsManaged for invalid IP
TEST_F(ManagedCodeCacheTest, IsManaged_InvalidIP_ReturnsFalse) {
    auto deadbeef = cache->IsManaged(0xDEADBEEF);
    EXPECT_TRUE(deadbeef.has_value());
    EXPECT_FALSE(deadbeef.value());
    auto zero = cache->IsManaged(0);
    EXPECT_TRUE(zero.has_value());
    EXPECT_FALSE(zero.value());
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

    cache->AddFunction(func1, /*isDynamic*/ false);
    cache->AddFunction(func2, /*isDynamic*/ false);
    EXPECT_EQ(func1, GetFuncIdOr0(code1Start + 0x50));
    EXPECT_EQ(func2, GetFuncIdOr0(code2Start + 0x50));

    // No cross-contamination
    auto outside = cache->GetFunctionInfo(code1Start + codeSize + 10);
    EXPECT_TRUE(outside.has_value());
    EXPECT_EQ(ManagedCodeCache::InvalidFunctionId, outside->FunctionId);
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
                cache->AddFunction(funcId, /*isDynamic*/ false);
            }
        });
    }

    for (auto& thread : threads) {
        thread.join();
    }

    // Verify every function is retrievable by IP
    for (int t = 0; t < numThreads; t++) {
        for (int i = 0; i < functionsPerThread; i++) {
            FunctionID funcId = (t * 1000) + i;
            uintptr_t codeStart = 0x10000 + (funcId * 0x1000);

            auto result = cache->GetFunctionInfo(codeStart + 0x50);
            EXPECT_TRUE(result.has_value())
                << "Function " << funcId << " not found at IP 0x" << std::hex << (codeStart + 0x50);
            EXPECT_EQ(funcId, result.has_value() ? result->FunctionId : 0)
                << "Wrong FunctionID for function " << funcId;

            EXPECT_TRUE(cache->IsManaged(codeStart + 0x50))
                << "IsManaged returned false for function " << funcId;
        }
    }

    // Verify an IP outside all registered ranges returns InvalidFunctionId
    auto outside = cache->GetFunctionInfo(0xDEAD);
    EXPECT_TRUE(outside.has_value());
    EXPECT_EQ(ManagedCodeCache::InvalidFunctionId, outside->FunctionId);
    auto outsideIsManaged = cache->IsManaged(0xDEAD);
    EXPECT_TRUE(outsideIsManaged.has_value());
    EXPECT_FALSE(outsideIsManaged.value());
}

// Test: IsManaged (no blocking)
TEST_F(ManagedCodeCacheTest, IsManaged_ConcurrentAccess) {
    FunctionID testFuncId = 999;
    uintptr_t codeStart = 0x7000;
    ULONG32 codeSize = 0x200;

    SetupMockCodeInfo(testFuncId, codeStart, codeSize);
    cache->AddFunction(testFuncId, /*isDynamic*/ false);
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
TEST_F(ManagedCodeCacheTest, GetFunctionInfo_BoundaryIPs_CorrectBehavior) {
    FunctionID testFuncId = 555;
    uintptr_t codeStart = 0x8000;
    ULONG32 codeSize = 0x100;

    SetupMockCodeInfo(testFuncId, codeStart, codeSize);
    cache->AddFunction(testFuncId, /*isDynamic*/ false);
    // Exact boundaries
    EXPECT_EQ(testFuncId, GetFuncIdOr0(codeStart));  // First byte (inclusive)
    EXPECT_EQ(testFuncId, GetFuncIdOr0(codeStart + codeSize - 1));  // Last byte (inclusive)

    // Just outside boundaries
    auto beforeStartIp = cache->GetFunctionInfo(codeStart - 1);
    EXPECT_TRUE(beforeStartIp.has_value());
    EXPECT_EQ(ManagedCodeCache::InvalidFunctionId, beforeStartIp->FunctionId);
    auto borderIp = cache->GetFunctionInfo(codeStart + codeSize);
    EXPECT_TRUE(borderIp.has_value());
    EXPECT_EQ(ManagedCodeCache::InvalidFunctionId, borderIp->FunctionId);
}

// Regression: GetCodeRanges used `startAddress + size - 1` without guarding
// size==0.  For startAddress==0 this gives endAddress==UINTPTR_MAX and the
// page-insertion loop `for (page=0; page<=UINTPTR_MAX/pageSize; ++page)` runs
// for billions of iterations, permanently hanging the (now synchronous) insert.
// For non-zero startAddress the loop silently skips (endPage < startPage) but
// the degenerate CodeRange still pollutes the sorted range vector.
TEST_F(ManagedCodeCacheTest, AddFunction_ZeroSizeRange_DoesNotPolluteCacheNonZeroBase) {
    FunctionID testFuncId = 777;
    uintptr_t codeStart = 0x9000;
    ULONG32 codeSize = 0;

    SetupMockCodeInfo(testFuncId, codeStart, codeSize);
    cache->AddFunction(testFuncId, /*isDynamic*/ false);
    // A zero-size range has no valid code; no IP should be reported as managed.
    auto codeStartIp = cache->IsManaged(codeStart);
    EXPECT_TRUE(codeStartIp.has_value());
    EXPECT_FALSE(codeStartIp.value())
        << "IP at startAddress of a size-0 range must not be managed";
    auto codeStartMinusOne = cache->IsManaged(codeStart - 1);
    EXPECT_TRUE(codeStartMinusOne.has_value());
    EXPECT_FALSE(codeStartMinusOne.value())
        << "IP just before startAddress must not be managed";
    auto codeStartPlusOneThousand = cache->IsManaged(codeStart + 0x1000);
    EXPECT_TRUE(codeStartPlusOneThousand.has_value());
    EXPECT_FALSE(codeStartPlusOneThousand.value())
        << "Unrelated IP above a size-0 range must not be managed";
}

// For startAddress==0 with size==0 the page loop would run from page 0 to
// ~UINTPTR_MAX/pageSize, hanging forever.  After the fix AddFunction (now
// synchronous) must return promptly and IsManaged must return false.
TEST_F(ManagedCodeCacheTest, AddFunction_ZeroSizeRangeAtAddressZero_DoesNotHang) {
    FunctionID testFuncId = 778;
    uintptr_t codeStart = 0;
    ULONG32 codeSize = 0;

    SetupMockCodeInfo(testFuncId, codeStart, codeSize);
    // If the synchronous insert is stuck this call blocks until the test times out.
    cache->AddFunction(testFuncId, /*isDynamic*/ false);

    auto codeStartPlusOneThousand = cache->IsManaged(0x1000);
    EXPECT_TRUE(codeStartPlusOneThousand.has_value());
    EXPECT_FALSE(codeStartPlusOneThousand.value())
        << "Unrelated IP above a size-0 range must not be managed";
}

// Test: Very large code range
TEST_F(ManagedCodeCacheTest, AddFunction_LargeCodeRange_WorksCorrectly) {
    FunctionID testFuncId = 888;
    uintptr_t codeStart = 0x100000;
    ULONG32 codeSize = 0x10000;  // 64KB

    SetupMockCodeInfo(testFuncId, codeStart, codeSize);
    cache->AddFunction(testFuncId, /*isDynamic*/ false);
    // Test various points in large range
    EXPECT_EQ(testFuncId, GetFuncIdOr0(codeStart));
    EXPECT_EQ(testFuncId, GetFuncIdOr0(codeStart + 0x8000));
    EXPECT_EQ(testFuncId, GetFuncIdOr0(codeStart + codeSize - 1));  // End
}

// Test: Null IP
TEST_F(ManagedCodeCacheTest, GetFunctionInfo_NullIP_ReturnsInvalidFunctionId) {
    auto nullIp = cache->GetFunctionInfo(0);
    EXPECT_TRUE(nullIp.has_value());
    EXPECT_EQ(ManagedCodeCache::InvalidFunctionId, nullIp->FunctionId);
}

// Test: GetCodeInfo2 failure handling
TEST_F(ManagedCodeCacheTest, AddFunction_GetCodeInfo2Fails_HandledGracefully) {
    FunctionID testFuncId = 999;

    EXPECT_CALL(*mockProfiler, GetCodeInfo2(testFuncId, _, _, _))
        .WillOnce(Return(E_FAIL));

    cache->AddFunction(testFuncId, /*isDynamic*/ false);
    // Should not crash
    auto nullIp = cache->GetFunctionInfo(0x1000);
    EXPECT_TRUE(nullIp.has_value());
    EXPECT_EQ(ManagedCodeCache::InvalidFunctionId, nullIp->FunctionId);
}

// Test: isDynamic given to AddFunction is returned by GetFunctionInfo
TEST_F(ManagedCodeCacheTest, AddFunction_DynamicMethod_IsDynamicPropagatedToLookup) {
    FunctionID testFuncId = 424242;
    uintptr_t codeStart = 0xE000;
    ULONG32 codeSize = 0x100;

    SetupMockCodeInfo(testFuncId, codeStart, codeSize);
    cache->AddFunction(testFuncId, /*isDynamic*/ true);

    auto info = cache->GetFunctionInfo(codeStart + 0x50);
    ASSERT_TRUE(info.has_value());
    EXPECT_EQ(testFuncId, info->FunctionId);
    EXPECT_TRUE(info->IsDynamic)
        << "isDynamic passed to AddFunction must be carried through to GetFunctionInfo";
}

// Test: the regular (non-dynamic) registration path reports IsDynamic == false.
TEST_F(ManagedCodeCacheTest, AddFunction_RegularMethod_IsDynamicFalse) {
    FunctionID testFuncId = 434343;
    uintptr_t codeStart = 0xF000;
    ULONG32 codeSize = 0x100;

    SetupMockCodeInfo(testFuncId, codeStart, codeSize);
    cache->AddFunction(testFuncId, /*isDynamic*/ false);

    auto info = cache->GetFunctionInfo(codeStart + 0x50);
    ASSERT_TRUE(info.has_value());
    EXPECT_EQ(testFuncId, info->FunctionId);
    EXPECT_FALSE(info->IsDynamic);
}

// Test: the R2R fallback path (GetFunctionFromIP_Original) always returns IsDynamic = false
// because precompiled code is never dynamic.
TEST_F(ManagedCodeCacheTest, GetFunctionInfo_R2RFallbackSucceeds_IsDynamicFalse) {
    uintptr_t r2rCodeStart = 0xB1000000;
    uintptr_t r2rCodeEnd   = 0xB100FFFF;
    uintptr_t ipInR2R      = r2rCodeStart + 0x500;
    FunctionID r2rFuncId = 0xABCD;

    std::vector<ModuleCodeRange> moduleRanges;
    moduleRanges.emplace_back(r2rCodeStart, r2rCodeEnd);
    cache->AddModuleRangesToCache(std::move(moduleRanges));

    EXPECT_CALL(*mockProfiler, GetFunctionFromIP(reinterpret_cast<LPCBYTE>(ipInR2R), _))
        .WillOnce([r2rFuncId](LPCBYTE, FunctionID* pFunctionId) -> HRESULT {
            *pFunctionId = r2rFuncId;
            return S_OK;
        });
    // On success, the range is synchronously added to the cache: this calls GetCodeInfo2
    SetupMockCodeInfo(r2rFuncId, r2rCodeStart, static_cast<ULONG32>(r2rCodeEnd - r2rCodeStart + 1));

    auto info = cache->GetFunctionInfo(ipInR2R);
    ASSERT_TRUE(info.has_value());
    EXPECT_EQ(r2rFuncId, info->FunctionId);
    EXPECT_FALSE(info->IsDynamic);
}

#ifdef _WINDOWS
// Test: On Windows, GetFunctionFromIP can crash (e.g. module unloaded concurrently).
// The SEH __try/__except in GetFunctionFromIP_Original must catch the access violation
// and GetFunctionInfo must return std::nullopt to signal the failure to the caller.
TEST_F(ManagedCodeCacheTest, GetFunctionInfo_GetFunctionFromIPRaisesAccessViolation_ReturnsNullopt) {
    // Register an R2R module range so that GetFunctionInfo falls through to
    // GetFunctionFromIP_Original (which wraps the ICorProfilerInfo call in __try/__except).
    uintptr_t r2rCodeStart = 0xB0000000;
    uintptr_t r2rCodeEnd   = 0xB000FFFF;
    uintptr_t ipInR2R      = r2rCodeStart + 0x500;

    std::vector<ModuleCodeRange> moduleRanges;
    moduleRanges.emplace_back(r2rCodeStart, r2rCodeEnd);
    cache->AddModuleRangesToCache(std::move(moduleRanges));

    // Simulate a crash during GetFunctionFromIP by raising an access violation.
    // This mirrors the real-world scenario where the CLR unloads the module containing
    // the target symbol while we are resolving the IP.
    EXPECT_CALL(*mockProfiler, GetFunctionFromIP(reinterpret_cast<LPCBYTE>(ipInR2R), _))
        .WillOnce([](LPCBYTE, FunctionID*) -> HRESULT {
            ::RaiseException(EXCEPTION_ACCESS_VIOLATION, 0, 0, nullptr);
            return S_OK; // unreachable
        });

    auto result = cache->GetFunctionInfo(ipInR2R);
    EXPECT_FALSE(result.has_value())
        << "GetFunctionInfo should return std::nullopt when GetFunctionFromIP raises an access violation";
}
#endif

// Test: IsManaged is lock-free - it must return a concrete value (and never block)
// while another thread holds the code ranges writer lock. This guards the
// contract relied on by the HybridUnwinder in a signal handler on ARM64.
TEST_F(ManagedCodeCacheTest, IsManaged_WriterHoldsCodeRangesLock_ReturnsValue) {
    FunctionID testFuncId = 321;
    uintptr_t codeStart = 0xC000;
    ULONG32 codeSize = 0x100;

    SetupMockCodeInfo(testFuncId, codeStart, codeSize);
    cache->AddFunction(testFuncId, /*isDynamic*/ false);

    std::atomic<bool> writerHoldsLock{false};
    std::atomic<bool> readerDone{false};
    std::thread writer([&]() {
        auto lock = cache->LockCodeRangesWriterForTest();
        writerHoldsLock.store(true);
        while (!readerDone.load())
        {
            std::this_thread::sleep_for(std::chrono::milliseconds(1));
        }
    });

    while (!writerHoldsLock.load())
    {
        std::this_thread::sleep_for(std::chrono::milliseconds(1));
    }

    auto managed = cache->IsManaged(codeStart + 0x50);
    ASSERT_TRUE(managed.has_value());
    EXPECT_TRUE(managed.value());
    auto notManaged = cache->IsManaged(0xDEADBEEF);
    ASSERT_TRUE(notManaged.has_value());
    EXPECT_FALSE(notManaged.value());
    EXPECT_EQ(testFuncId, GetFuncIdOr0(codeStart + 0x50));

    readerDone.store(true);
    writer.join();
}

// Test: same as above for the R2R modules writer lock (IP not in any JIT range,
// so IsManaged falls through to the module lookup).
TEST_F(ManagedCodeCacheTest, IsManaged_WriterHoldsModulesLock_ReturnsValue) {
    const uintptr_t r2rCodeStart = 0xA0000000;
    const uintptr_t r2rCodeEnd = 0xA000FFFF;
    cache->AddModuleRangesToCache({{r2rCodeStart, r2rCodeEnd}});

    std::atomic<bool> writerHoldsLock{false};
    std::atomic<bool> readerDone{false};
    std::thread writer([&]() {
        auto lock = cache->LockModulesWriterForTest();
        writerHoldsLock.store(true);
        while (!readerDone.load())
        {
            std::this_thread::sleep_for(std::chrono::milliseconds(1));
        }
    });

    while (!writerHoldsLock.load())
    {
        std::this_thread::sleep_for(std::chrono::milliseconds(1));
    }

    auto inModule = cache->IsManaged(r2rCodeStart + 0x500);
    ASSERT_TRUE(inModule.has_value());
    EXPECT_TRUE(inModule.value());
    auto outside = cache->IsManaged(0xDEADBEEF);
    ASSERT_TRUE(outside.has_value());
    EXPECT_FALSE(outside.value());

    readerDone.store(true);
    writer.join();
}

// Test: readers running concurrently with a writer never miss a range that was
// added before they started looking for it, and never see a torn range. The writer
// inserts out of order (forcing block copies and memory reclamation) and in order
// (in-place appends), on a few pages shared by all the functions.
TEST_F(ManagedCodeCacheTest, IsManaged_ConcurrentWriter_NoFalseNegative) {
    constexpr int NbFunctions = 4000;
    constexpr uintptr_t Base = 0x7F0000000000;
    constexpr uintptr_t Stride = 0x40;
    constexpr uintptr_t Size = 0x30;

    // Functions i are laid out at Base + i * Stride, but added in a shuffled order
    auto AddressOf = [](int i) { return Base + static_cast<uintptr_t>(i) * Stride; };
    // 3 functions out of 4 are added in address order (in-place appends), then the
    // remaining ones in reverse order (each one is inserted in the middle of a block)
    std::vector<int> order;
    order.reserve(NbFunctions);
    for (int i = 0; i < NbFunctions; i++)
    {
        if (i % 4 != 3)
        {
            order.push_back(i);
        }
    }
    for (int i = NbFunctions - 1; i >= 0; i--)
    {
        if (i % 4 == 3)
        {
            order.push_back(i);
        }
    }

    std::atomic<bool> stop{false};
    std::atomic<int> failures{0};
    std::atomic<std::uint64_t> lookups{0};

    std::vector<std::atomic<int>> addedAt(NbFunctions);
    for (auto& a : addedAt)
    {
        a.store(-1);
    }

    std::vector<std::thread> readers;
    for (int t = 0; t < 4; t++)
    {
        readers.emplace_back([&, t]() {
            std::uint64_t seed = 0x9E3779B97F4A7C15ull * (t + 1);
            while (!stop.load(std::memory_order_relaxed))
            {
                seed ^= seed << 13;
                seed ^= seed >> 7;
                seed ^= seed << 17;
                int i = static_cast<int>(seed % NbFunctions);
                bool wasAdded = addedAt[i].load(std::memory_order_acquire) >= 0;

                auto ip = AddressOf(i) + (seed >> 32) % Size;
                auto managed = cache->IsManaged(ip);
                if (!managed.has_value() || (wasAdded && !managed.value()))
                {
                    failures++;
                }

                // The gap between two functions must never be reported as managed
                auto gap = cache->IsManaged(AddressOf(i) + Size + 1);
                if (!gap.has_value() || gap.value())
                {
                    failures++;
                }
                lookups++;
            }
        });
    }

    for (int n = 0; n < NbFunctions; n++)
    {
        auto i = order[n];
        cache->AddFunctionRangesToCache({CodeRange{AddressOf(i), AddressOf(i) + Size - 1, static_cast<uintptr_t>(i + 1), false}});
        addedAt[i].store(n, std::memory_order_release);
    }

    stop = true;
    for (auto& reader : readers)
    {
        reader.join();
    }

    EXPECT_EQ(0, failures.load());
    EXPECT_GT(lookups.load(), 0u);

    for (int i = 0; i < NbFunctions; i++)
    {
        EXPECT_EQ(static_cast<FunctionID>(i + 1), GetFuncIdOr0(AddressOf(i) + 1));
    }
}

// Test: memory retired while a reader was active is reclaimed by GetFunctionInfo
// (profiler thread) without waiting for the next write
TEST_F(ManagedCodeCacheTest, GetFunctionInfo_ReclaimsRetiredMemory) {
    const uintptr_t base = 0x7E0000000000;
    cache->AddFunctionRangesToCache({CodeRange{base + 0x100, base + 0x1FF, 1, false}});

    {
        auto scope = cache->EnterReadScope();
        // Out of order: the page block is copied and the old one retired, but it
        // cannot be freed while the scope is held
        cache->AddFunctionRangesToCache({CodeRange{base, base + 0xFF, 2, false}});
        EXPECT_GT(cache->PendingReclaimBytesForTest(), 0u);
        EXPECT_TRUE(cache->IsManaged(base + 0x10, scope).value());
    }

    EXPECT_GT(cache->PendingReclaimBytesForTest(), 0u);
    EXPECT_EQ(2u, GetFuncIdOr0(base + 0x10));
    EXPECT_EQ(0u, cache->PendingReclaimBytesForTest());
}

// Test: module ranges are removed on RemoveModule and can be re-added at the same place
TEST_F(ManagedCodeCacheTest, IsManaged_ModuleRemovedThenReAdded) {
    const uintptr_t start = 0xB0000000;
    const uintptr_t end = 0xB000FFFF;

    cache->AddModuleRangesToCache({{start, end}});
    EXPECT_TRUE(cache->IsManaged(start + 0x10).value());

    cache->RemoveModuleRangesFromCache({{start, end}});
    EXPECT_FALSE(cache->IsManaged(start + 0x10).value());

    cache->AddModuleRangesToCache({{start, end}});
    EXPECT_TRUE(cache->IsManaged(start + 0x10).value());
}

// Test: ranges outside of the indexable address space (>= 2^52) are ignored
TEST_F(ManagedCodeCacheTest, AddFunction_RangeAboveIndexableAddressSpace_Ignored) {
    const uintptr_t highStart = uintptr_t{1} << 52;
    cache->AddFunctionRangesToCache({CodeRange{highStart, highStart + 0x100, 42, false}});
    cache->AddModuleRangesToCache({{highStart, highStart + 0x1000}});

    auto managed = cache->IsManaged(highStart + 0x10);
    ASSERT_TRUE(managed.has_value());
    EXPECT_FALSE(managed.value());

    // PAC-signed / kernel-like addresses
    EXPECT_FALSE(cache->IsManaged(0xFFFF800000001000).value());
}

// Test: IsManaged falls back to R2R module check when IP is not in the JIT page map
TEST_F(ManagedCodeCacheTest, IsManaged_IPInR2RModule_NotInPageMap_ReturnsTrue) {
    // Register an R2R module range directly (bypassing PE parsing)
    // Use an address that has no JIT-compiled code registered on its page
    uintptr_t r2rCodeStart = 0xA0000000;
    uintptr_t r2rCodeEnd   = 0xA000FFFF;

    std::vector<ModuleCodeRange> moduleRanges;
    moduleRanges.emplace_back(r2rCodeStart, r2rCodeEnd);

    cache->AddModuleRangesToCache(std::move(moduleRanges));

    // An IP within the R2R range but with no JIT page entry should still be detected as managed
    uintptr_t ipInR2R = r2rCodeStart + 0x500;
    EXPECT_TRUE(cache->IsManaged(ipInR2R))
        << "IsManaged should return true for an IP in an R2R module even when the JIT page map has no entry for that page";

    // An IP outside both the JIT page map and any R2R module should still be false
    auto outsideIsManaged = cache->IsManaged(0xDEADBEEF);
    EXPECT_TRUE(outsideIsManaged.has_value());
    EXPECT_FALSE(outsideIsManaged.value());
}
