// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "CdacLoaderContract.h"

namespace cdac
{
bool LoaderContract::IsLoaderSupported() const
{
    return (_target.HasGlobal("SystemDomain") || _target.HasGlobal("AppDomain")) && _target.HasType("LoaderAllocator");
}

bool LoaderContract::IsCodeHeapSupported() const
{
    return _target.HasGlobal("EEJitManagerAddress") && _target.HasType("EEJitManager") && _target.HasType("CodeHeapListNode") && _target.HasType("CodeHeap");
}

uintptr_t LoaderContract::GetGlobalLoaderAllocator()
{
    if (!_target.HasGlobal("SystemDomain") || !_target.HasType("SystemDomain"))
    {
        return 0;
    }

    uintptr_t systemDomain = _target.ReadPointer(_target.ReadGlobalPointer("SystemDomain"));
    if (systemDomain == 0)
    {
        return 0;
    }

    // GlobalLoaderAllocator is an inline LoaderAllocator; its address is the LA pointer.
    return _target.FieldAddress(systemDomain, "SystemDomain", "GlobalLoaderAllocator");
}

void LoaderContract::EnumerateAssemblies(const std::function<void(uintptr_t)>& sink)
{
    if (!_target.HasGlobal("AppDomain") || !_target.HasType("AppDomain") || !_target.HasType("Assembly"))
    {
        return;
    }

    uintptr_t appDomain = _target.ReadPointer(_target.ReadGlobalPointer("AppDomain"));
    if (appDomain == 0)
    {
        return;
    }

    // AppDomain.AssemblyList is an inline ArrayListBase of Assembly*.
    uintptr_t assemblyList = _target.FieldAddress(appDomain, "AppDomain", "AssemblyList");
    WalkArrayList(assemblyList, sink);
}

void LoaderContract::EnumerateModules(const std::function<void(uintptr_t)>& sink)
{
    EnumerateAssemblies([&](uintptr_t assembly) {
        uintptr_t module = _target.ReadFieldPointer(assembly, "Assembly", "Module");
        if (module != 0)
        {
            sink(module);
        }
    });
}

uintptr_t LoaderContract::GetModuleLoaderAllocator(uintptr_t module)
{
    if (module == 0 || !_target.HasField("Module", "LoaderAllocator"))
    {
        return 0;
    }
    return _target.ReadFieldPointer(module, "Module", "LoaderAllocator");
}

uintptr_t LoaderContract::GetModuleThunkHeap(uintptr_t module)
{
    // ThunkHeap is not exposed by every descriptor; best effort only.
    if (module == 0 || !_target.HasField("Module", "ThunkHeap"))
    {
        return 0;
    }
    return _target.ReadFieldPointer(module, "Module", "ThunkHeap");
}

void LoaderContract::EnumerateLoaderAllocatorHeaps(uintptr_t loaderAllocator, const Sink& sink)
{
    if (loaderAllocator == 0 || !_target.HasType("LoaderAllocator"))
    {
        return;
    }

    GetLoaderAllocatorHeaps(loaderAllocator, [&](NativeHeapKind kind, uintptr_t heap) {
        WalkLoaderHeap(heap, kind, sink);
    });
}

void LoaderContract::WalkLoaderHeap(uintptr_t loaderHeap, NativeHeapKind kind, const Sink& sink)
{
    if (loaderHeap == 0 || !_target.HasType("LoaderHeap") || !_target.HasType("LoaderHeapBlock"))
    {
        return;
    }

    uintptr_t block = _target.ReadFieldPointer(loaderHeap, "LoaderHeap", "FirstBlock");
    int guard = 0;
    while (block != 0 && guard++ < MaxListIterations)
    {
        uintptr_t address = _target.ReadFieldPointer(block, "LoaderHeapBlock", "VirtualAddress");
        uintptr_t size = _target.ReadFieldPointer(block, "LoaderHeapBlock", "VirtualSize");
        uintptr_t next = _target.ReadFieldPointer(block, "LoaderHeapBlock", "Next");

        if (address != 0)
        {
            ClrNativeHeapInfo info;
            info.Address = address;
            info.Size = static_cast<uint64_t>(size); // LoaderHeapBlock exposes the reserved VirtualSize
            info.Committed = _target.ProbeCommitted(address, info.Size);
            info.Kind = kind;
            info.State = NativeHeapState::Active;
            sink(info);
        }

        if (next == block)
        {
            break; // self-referential = corrupt; stop
        }
        block = next;
    }
}

void LoaderContract::GetLoaderAllocatorHeaps(uintptr_t la, const std::function<void(NativeHeapKind, uintptr_t)>& sink)
{
    auto readLaHeap = [&](const char* field) -> uintptr_t {
        return _target.ReadFieldPointer(la, "LoaderAllocator", field);
    };

    sink(NativeHeapKind::LowFrequencyHeap, readLaHeap("LowFrequencyHeap"));
    sink(NativeHeapKind::HighFrequencyHeap, readLaHeap("HighFrequencyHeap"));
    if (_target.HasField("LoaderAllocator", "StaticsHeap"))
    {
        sink(NativeHeapKind::StaticsHeap, readLaHeap("StaticsHeap"));
    }
    sink(NativeHeapKind::StubHeap, readLaHeap("StubHeap"));
    sink(NativeHeapKind::ExecutableHeap, readLaHeap("ExecutableHeap"));

    if (_target.HasField("LoaderAllocator", "FixupPrecodeHeap"))
    {
        sink(NativeHeapKind::FixupPrecodeHeap, readLaHeap("FixupPrecodeHeap"));
    }
    if (_target.HasField("LoaderAllocator", "NewStubPrecodeHeap"))
    {
        sink(NativeHeapKind::NewStubPrecodeHeap, readLaHeap("NewStubPrecodeHeap"));
    }
    if (_target.HasField("LoaderAllocator", "DynamicHelpersStubHeap"))
    {
        sink(NativeHeapKind::DynamicHelpersStubHeap, readLaHeap("DynamicHelpersStubHeap"));
    }

    if (_target.HasField("LoaderAllocator", "VirtualCallStubManager") && _target.HasType("VirtualCallStubManager"))
    {
        uintptr_t vcs = _target.ReadFieldPointer(la, "LoaderAllocator", "VirtualCallStubManager");
        if (vcs != 0)
        {
            if (_target.HasField("VirtualCallStubManager", "IndcellHeap"))
            {
                sink(NativeHeapKind::IndirectionCellHeap, _target.ReadFieldPointer(vcs, "VirtualCallStubManager", "IndcellHeap"));
            }
            if (_target.HasField("VirtualCallStubManager", "CacheEntryHeap"))
            {
                sink(NativeHeapKind::CacheEntryHeap, _target.ReadFieldPointer(vcs, "VirtualCallStubManager", "CacheEntryHeap"));
            }
        }
    }
}

void LoaderContract::WalkArrayList(uintptr_t arrayListBase, const std::function<void(uintptr_t)>& sink)
{
    if (!_target.HasType("ArrayListBase") || !_target.HasType("ArrayListBlock"))
    {
        return;
    }

    uint32_t count = _target.ReadField<uint32_t>(arrayListBase, "ArrayListBase", "Count");
    uintptr_t block = _target.FieldAddress(arrayListBase, "ArrayListBase", "FirstBlock");

    uint32_t found = 0;
    int guard = 0;
    while (block != 0 && found < count && guard++ < MaxListIterations)
    {
        uint32_t size = _target.ReadField<uint32_t>(block, "ArrayListBlock", "Size");
        uintptr_t arrayStart = _target.FieldAddress(block, "ArrayListBlock", "ArrayStart");

        for (uint32_t i = 0; i < size && found < count; i++)
        {
            uintptr_t element = _target.ReadPointer(arrayStart + static_cast<uintptr_t>(i) * static_cast<uintptr_t>(_target.PointerSize()));
            found++;
            if (element != 0)
            {
                sink(element);
            }
        }

        block = _target.ReadFieldPointer(block, "ArrayListBlock", "Next");
    }
}

void LoaderContract::EnumerateCodeHeaps(const Sink& sink)
{
    if (!IsCodeHeapSupported())
    {
        return;
    }

    uintptr_t eeJitManager = _target.ReadPointer(_target.ReadGlobalPointer("EEJitManagerAddress"));
    if (eeJitManager == 0)
    {
        return;
    }

    uintptr_t node = _target.ReadFieldPointer(eeJitManager, "EEJitManager", "AllCodeHeaps");
    int guard = 0;
    while (node != 0 && guard++ < MaxListIterations)
    {
        uintptr_t heap = _target.ReadFieldPointer(node, "CodeHeapListNode", "Heap");
        uintptr_t next = _target.ReadFieldPointer(node, "CodeHeapListNode", "Next");

        if (heap != 0)
        {
            DescribeCodeHeap(heap, sink);
        }

        if (next == node)
        {
            break;
        }
        node = next;
    }
}

void LoaderContract::DescribeCodeHeap(uintptr_t codeHeap, const Sink& sink)
{
    uint8_t heapType = _target.HasField("CodeHeap", "HeapType")
                           ? _target.ReadField<uint8_t>(codeHeap, "CodeHeap", "HeapType")
                           : static_cast<uint8_t>(0xff);

    if (heapType == LoaderCodeHeapType && _target.HasType("LoaderCodeHeap"))
    {
        // LoaderHeap is an ExplicitControlLoaderHeap embedded INLINE in the LoaderCodeHeap; its
        // address is the field address, NOT a dereferenced pointer. (Dereferencing it produces
        // petabyte-sized garbage regions.)
        uintptr_t loaderHeap = _target.FieldAddress(codeHeap, "LoaderCodeHeap", "LoaderHeap");
        WalkLoaderHeap(loaderHeap, NativeHeapKind::LoaderCodeHeap, sink);
    }
    else if (heapType == HostCodeHeapType && _target.HasType("HostCodeHeap"))
    {
        uintptr_t baseAddress = _target.ReadFieldPointer(codeHeap, "HostCodeHeap", "BaseAddress");
        uintptr_t current = _target.ReadFieldPointer(codeHeap, "HostCodeHeap", "CurrentAddress");
        uint64_t length = current >= baseAddress ? static_cast<uint64_t>(current - baseAddress) : 0;

        ClrNativeHeapInfo info;
        info.Address = baseAddress;
        info.Size = length;
        info.Committed = _target.ProbeCommitted(baseAddress, length);
        info.Kind = NativeHeapKind::HostCodeHeap;
        info.State = NativeHeapState::Active;
        sink(info);
    }
}
} // namespace cdac
