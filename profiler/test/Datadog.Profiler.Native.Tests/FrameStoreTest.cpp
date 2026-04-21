// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gtest/gtest.h"
#include "gmock/gmock.h"

#include "FrameStore.h"
#include "ManagedCodeCache.h"
#include "MockProfilerInfo.h"

#include <memory>
#include <string>

using namespace testing;

namespace {

// The string produced by FrameStore::GetFrame when an IP cannot be resolved to a managed
// function (see FrameStore.cpp). Kept in sync with the private constant in the .cpp file.
// If the production string changes, update this constant so the test intent stays explicit.
constexpr const char* NotResolvedFrameText =
    "|lm:Unknown-Assembly |ns: |ct:Unknown-Type |cg: |fn:NotResolvedFrame |fg: |sg:(?)";

} // namespace

// These tests guard the contract that FrameStore::GetFrame uses to tell
// RawSampleTransformer whether a frame is resolved (kept) or not (dropped):
//   - pair<true, ...>  => keep the frame in the sample
//   - pair<false, ...> => drop the frame from the sample
//
// Native instruction pointers (addresses that don't belong to any managed method)
// MUST be reported as "not resolved" (false) so that they are filtered out by the
// transformer. Regressing this behavior causes long runs of "NotResolvedFrame"
// entries at the top of exception/walltime profiles on platforms that unwind
// native frames before reaching managed frames (notably Linux ARM64, where the
// HybridUnwinder walks native frames via libunwind before transitioning to
// managed frames via frame-pointer unwinding).
//
// Two parallel code paths exist in FrameStore::GetFrame:
//   * no ManagedCodeCache: asks ICorProfilerInfo::GetFunctionFromIP directly
//   * with ManagedCodeCache: asks the cache for the FunctionID
// Both must behave the same way for native IPs.

// Test 2: No-cache path - a native IP (FAILED hr from GetFunctionFromIP) must be
// reported as not resolved so RawSampleTransformer drops it from the sample.
TEST(FrameStoreTest, GetFrame_NoCache_NativeIp_ReturnsNotResolvedAndDropped)
{
    auto mockProfiler = MockProfilerInfo{};

    EXPECT_CALL(mockProfiler, GetFunctionFromIP(_, _))
        .WillRepeatedly(Return(E_FAIL));

    FrameStore frameStore(
        /*pCorProfilerInfo*/ &mockProfiler,
        /*pConfiguration  */ nullptr,
        /*pDebugInfoStore */ nullptr,
        /*pManagedCodeCache*/ nullptr);

    // IP must be greater than FrameStore::MaxFakeIP so we don't hit the fake-IP
    // short-circuit at the top of GetFrame.
    const uintptr_t nativeIp = 0x12345;

    auto [isResolved, frameInfo] = frameStore.GetFrame(nativeIp);

    EXPECT_FALSE(isResolved) << "Native IPs must be reported as unresolved so "
                                "RawSampleTransformer drops them from the sample.";
    EXPECT_EQ(std::string(frameInfo.Frame), std::string(NotResolvedFrameText));
}

// Test 5: Cached path - a native IP that is not in any managed code range must be
// reported as not resolved. The ManagedCodeCache returns InvalidFunctionId for
// such IPs; FrameStore must translate that to isResolved == false.
//
// This is the regression guard for the ARM64 exception-profiling issue where a
// long sequence of "NotResolvedFrame" entries leaked into the sample because
// FrameStore was returning isResolved == true for InvalidFunctionId.
TEST(FrameStoreTest, GetFrame_WithCache_NativeIp_ReturnsNotResolvedAndDropped)
{
    auto mockProfiler = MockProfilerInfo{};

    // Empty cache => any IP resolves to InvalidFunctionId (there are no registered
    // JIT ranges and no R2R modules), which is exactly the "native IP" case.
    auto cache = std::make_unique<ManagedCodeCache>(&mockProfiler);
    cache->Initialize();

    FrameStore frameStore(
        /*pCorProfilerInfo*/ &mockProfiler,
        /*pConfiguration  */ nullptr,
        /*pDebugInfoStore */ nullptr,
        /*pManagedCodeCache*/ cache.get());

    // Sanity-check the upstream contract we rely on: the cache must report the IP
    // as "definitely native" (a value equal to InvalidFunctionId), not nullopt.
    const uintptr_t nativeIp = 0xDEAD;
    auto cacheResult = cache->GetFunctionId(nativeIp);
    ASSERT_TRUE(cacheResult.has_value());
    ASSERT_EQ(ManagedCodeCache::InvalidFunctionId, cacheResult.value());

    auto [isResolved, frameInfo] = frameStore.GetFrame(nativeIp);

    EXPECT_FALSE(isResolved) << "Native IPs (InvalidFunctionId from the cache) must "
                                "be reported as unresolved so RawSampleTransformer "
                                "drops them from the sample.";
    EXPECT_EQ(std::string(frameInfo.Frame), std::string(NotResolvedFrameText));

    cache.reset();
}
