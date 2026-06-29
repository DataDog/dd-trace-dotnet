// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "CdacTarget.h"
#include "ClrNativeHeapInfo.h"

#include <functional>
#include <vector>

namespace cdac
{
// Implementation of the Loader data contract's native-heap surface against the cDAC Target (no DAC,
// no ClrMD), following dotnet/runtime's Loader_1 algorithms. It also walks the JIT code heaps
// (EEJitManager.AllCodeHeaps) since that walk reuses WalkLoaderHeap. Optional fields/globals are
// Has*-guarded so the contract degrades gracefully across runtime builds.
//
// Output uses a sink callback to replace C# "yield return" (no coroutines pre-C++20).
class LoaderContract
{
public:
    using Sink = std::function<void(const ClrNativeHeapInfo&)>;

    explicit LoaderContract(Target& target) :
        _target(target)
    {
    }

    // True when the descriptor exposes the basic loader globals/types this contract needs.
    bool IsLoaderSupported() const;

    // True when the descriptor exposes the EEJitManager global and code-heap types.
    bool IsCodeHeapSupported() const;

    // --- loader heaps ---
    uintptr_t GetGlobalLoaderAllocator();
    void EnumerateModules(const std::function<void(uintptr_t)>& sink);
    uintptr_t GetModuleLoaderAllocator(uintptr_t module);
    uintptr_t GetModuleThunkHeap(uintptr_t module);
    void EnumerateLoaderAllocatorHeaps(uintptr_t loaderAllocator, const Sink& sink);
    void WalkLoaderHeap(uintptr_t loaderHeap, NativeHeapKind kind, const Sink& sink);

    // --- JIT code heaps ---
    void EnumerateCodeHeaps(const Sink& sink);

private:
    static constexpr int MaxListIterations = 1 << 20;

    // Mirrors CodeHeap::CodeHeapType in codeman.h.
    static constexpr uint8_t LoaderCodeHeapType = 0;
    static constexpr uint8_t HostCodeHeapType = 1;

    void EnumerateAssemblies(const std::function<void(uintptr_t)>& sink);
    void WalkArrayList(uintptr_t arrayListBase, const std::function<void(uintptr_t)>& sink);
    void GetLoaderAllocatorHeaps(uintptr_t loaderAllocator,
                                 const std::function<void(NativeHeapKind, uintptr_t)>& sink);
    void DescribeCodeHeap(uintptr_t codeHeap, const Sink& sink);

    Target& _target;
};
} // namespace cdac
