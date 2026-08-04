// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

// Vendored, verbatim, from dotnet/runtime src/coreclr/inc/sospriv.idl: the pieces that the
// prebuilt/vendored sospriv.h predates (it stops at ISOSDacInterface12). This is a Dac-only header:
// include it ONLY from the Dac*.cpp translation units, AFTER clrdata.h / sospriv.h (which provide
// CLRDATA_ADDRESS, IUnknown, ISOSEnum, VISITHEAP and the MIDL_INTERFACE/STDMETHODCALLTYPE macros).
//
// We only call the three GC memory-region enumerators (GetGCFreeRegions /
// GetHandleTableMemoryRegions / GetGCBookkeepingMemoryRegions) and ISOSMemoryEnum::Next, but ALL
// members of ISOSDacInterface13 are declared in their exact upstream order so the vtable layout is
// correct.

// sospriv.idl: typedef int LoaderHeapKind; + cpp_quote enum. Needed only for the TraverseLoaderHeap
// / GetLoaderAllocatorHeaps signatures (which we never call) to keep the vtable correct.
#ifndef _SOS_LoaderHeapKind_
#define _SOS_LoaderHeapKind_
typedef enum
{
    LoaderHeapKindNormal = 0,
    LoaderHeapKindExplicitControl = 1
} LoaderHeapKind;
#endif // _SOS_LoaderHeapKind_

#ifndef _SOS_MemoryRegion_
#define _SOS_MemoryRegion_
typedef struct _SOSMemoryRegion
{
    CLRDATA_ADDRESS Start;
    CLRDATA_ADDRESS Size;
    CLRDATA_ADDRESS ExtraData;
    int Heap;
} SOSMemoryRegion;
#endif // _SOS_MemoryRegion_

// uuid(E4B860EC-337A-40C0-A591-F09A9680690F)
MIDL_INTERFACE("E4B860EC-337A-40C0-A591-F09A9680690F")
ISOSMemoryEnum : public ISOSEnum
{
public:
    virtual HRESULT STDMETHODCALLTYPE Next(
        /* [in] */ unsigned int count,
        /* [out] */ SOSMemoryRegion memRegion[],
        /* [out] */ unsigned int* pNeeded) = 0;
};

// uuid(3176a8ed-597b-4f54-a71f-83695c6a8c5e)
MIDL_INTERFACE("3176a8ed-597b-4f54-a71f-83695c6a8c5e")
ISOSDacInterface13 : public IUnknown
{
public:
    virtual HRESULT STDMETHODCALLTYPE TraverseLoaderHeap(
        CLRDATA_ADDRESS loaderHeapAddr,
        LoaderHeapKind kind,
        VISITHEAP pCallback) = 0;
    virtual HRESULT STDMETHODCALLTYPE GetDomainLoaderAllocator(
        CLRDATA_ADDRESS domainAddress,
        CLRDATA_ADDRESS* pLoaderAllocator) = 0;
    virtual HRESULT STDMETHODCALLTYPE GetLoaderAllocatorHeapNames(
        int count,
        const char** ppNames,
        int* pNeeded) = 0;
    virtual HRESULT STDMETHODCALLTYPE GetLoaderAllocatorHeaps(
        CLRDATA_ADDRESS loaderAllocator,
        int count,
        CLRDATA_ADDRESS* pLoaderHeaps,
        LoaderHeapKind* pKinds,
        int* pNeeded) = 0;
    virtual HRESULT STDMETHODCALLTYPE GetHandleTableMemoryRegions(
        ISOSMemoryEnum** ppEnum) = 0;
    virtual HRESULT STDMETHODCALLTYPE GetGCBookkeepingMemoryRegions(
        ISOSMemoryEnum** ppEnum) = 0;
    virtual HRESULT STDMETHODCALLTYPE GetGCFreeRegions(
        ISOSMemoryEnum** ppEnum) = 0;
    virtual HRESULT STDMETHODCALLTYPE LockedFlush() = 0;
};
