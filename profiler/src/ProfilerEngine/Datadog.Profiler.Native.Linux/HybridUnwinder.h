// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "IUnwinder.h"

#include <atomic>
#include <optional>

class Callstack;
class ManagedCodeCache;
class StackDeltaMap;

class HybridUnwinder: public IUnwinder
{
public:
    HybridUnwinder(ManagedCodeCache* managedCodeCache,
                   const std::atomic<StackDeltaMap*>* deltaMapAtomicPtr);
    ~HybridUnwinder() override = default;

    std::int32_t Unwind(void* ctx, Callstack& callstack,
                        std::uintptr_t stackBase = 0, std::uintptr_t stackEnd = 0,
                        UnwindingRecorder* recorder = nullptr) const override;

private:
    // Phase 1: Walk native frames using pre-computed stack deltas.
    // Returns true if we found managed code and should continue to Phase 2.
    // On return, outIp/outFp are set to the IP/FP at the managed transition point.
    bool UnwindNativeFrames(void* ctx, Callstack& callstack, UnwindingRecorder* recorder,
                            std::uintptr_t& outIp, std::uintptr_t& outFp) const;

    void UnwindManagedFrames(std::uintptr_t ip, std::uintptr_t fp,
                             Callstack& callstack, UnwindingRecorder* recorder,
                             std::uintptr_t stackBase, std::uintptr_t stackEnd) const;

    ManagedCodeCache* _codeCache;
    // Points to the atomic StackDeltaMap* owned by LibrariesInfoCache.
    // HybridUnwinder does an atomic load from the signal handler to get
    // the current immutable delta map.
    const std::atomic<StackDeltaMap*>* _deltaMapAtomicPtr;
};
