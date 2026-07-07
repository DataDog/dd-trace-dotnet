// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "CdacNativeHeapEnumerator.h"

#include "CdacGCContract.h"
#include "CdacLoaderContract.h"
#include "CdacTarget.h"
#include "ContractDescriptorLocator.h"
#include "Log.h"
#include "LogicalDescriptor.h"

#include <set>

CdacNativeHeapEnumerator::CdacNativeHeapEnumerator()
{
    uintptr_t descriptorAddress = 0;
    if (!cdac::ContractDescriptorLocator::TryLocate(descriptorAddress))
    {
        Log::Debug("!eeheap (cdac): DotNetRuntimeContractDescriptor symbol not found; cDAC backend unavailable.");
        return;
    }

    cdac::LogicalDescriptor descriptor;
    if (!descriptor.Build(_reader, descriptorAddress))
    {
        Log::Debug("!eeheap (cdac): failed to read/validate the contract descriptor; cDAC backend unavailable.");
        return;
    }

    _target = std::make_unique<cdac::Target>(_reader, std::move(descriptor));
    _available = true;
}

CdacNativeHeapEnumerator::~CdacNativeHeapEnumerator() = default;

bool CdacNativeHeapEnumerator::IsAvailable() const
{
    return _available;
}

std::vector<ClrNativeHeapInfo> CdacNativeHeapEnumerator::EnumerateAll()
{
    std::vector<ClrNativeHeapInfo> results;
    if (!_available || _target == nullptr)
    {
        return results;
    }

    auto& target = *_target;

    // A sub-descriptor slot (e.g. the GC's) can transition null -> real-address after we first read
    // the descriptor, so re-scan pending slots on this live target before enumerating GC regions.
    if (target.PendingSubDescriptorCount() > 0)
    {
        target.Refresh();
    }

    const auto& contracts = target.Contracts();
    auto hasContract = [&](const char* name) {
        return contracts.find(name) != contracts.end();
    };

    auto sink = [&](const ClrNativeHeapInfo& info) {
        results.push_back(info);
    };

    // Wrap each source so a single corrupt structure degrades to "skip this source".
    auto safe = [](const std::function<void()>& work) {
        try
        {
            work();
        }
        catch (...)
        {
        }
    };

    cdac::LoaderContract loader(target);
    cdac::GCContract gc(target);

    // 1. JIT code heaps.
    if (hasContract("ExecutionManager") && loader.IsCodeHeapSupported())
    {
        safe([&] { loader.EnumerateCodeHeaps(sink); });
    }

    // 2. Loader-allocator heaps (SystemDomain + every module), deduped by LoaderAllocator pointer.
    if (hasContract("Loader") && loader.IsLoaderSupported())
    {
        std::set<uintptr_t> visited;

        uintptr_t globalLoaderAllocator = 0;
        safe([&] { globalLoaderAllocator = loader.GetGlobalLoaderAllocator(); });
        if (globalLoaderAllocator != 0 && visited.insert(globalLoaderAllocator).second)
        {
            safe([&] { loader.EnumerateLoaderAllocatorHeaps(globalLoaderAllocator, sink); });
        }

        std::vector<uintptr_t> modules;
        safe([&] { loader.EnumerateModules([&](uintptr_t m) { modules.push_back(m); }); });

        for (uintptr_t module : modules)
        {
            uintptr_t loaderAllocator = 0;
            safe([&] { loaderAllocator = loader.GetModuleLoaderAllocator(module); });
            if (loaderAllocator != 0 && visited.insert(loaderAllocator).second)
            {
                safe([&] { loader.EnumerateLoaderAllocatorHeaps(loaderAllocator, sink); });
            }

            uintptr_t thunkHeap = 0;
            safe([&] { thunkHeap = loader.GetModuleThunkHeap(module); });
            if (thunkHeap != 0 && visited.insert(thunkHeap).second)
            {
                safe([&] { loader.WalkLoaderHeap(thunkHeap, NativeHeapKind::ThunkHeap, sink); });
            }
        }
    }

    // 3. GC native regions: per-generation allocated segments + free regions, handle-table
    //    segments and bookkeeping (parity with the DAC backend).
    if (hasContract("GC"))
    {
        safe([&] { gc.GetGCHeapSegments(sink); });
        safe([&] { gc.GetGCFreeRegions(sink); });
        safe([&] { gc.GetHandleTableMemoryRegions(sink); });
        safe([&] { gc.GetGCBookkeepingMemoryRegions(sink); });
    }

    return results;
}
