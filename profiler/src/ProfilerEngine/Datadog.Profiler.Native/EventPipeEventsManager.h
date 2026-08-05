// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

// from dotnet coreclr includes
#include "cor.h"
#include "corprof.h"
// end

#include <memory>
#include <shared_mutex>
#include <unordered_map>

#include "BclEventsParser.h"
#include "ClrEventsParser.h"
#include "DotnetEventsProvider.h"


class IAllocationsListener;
class IContentionListener;
class IGCSuspensionsListener;
class IGarbageCollectionsListener;
class INetworkListener;
class IConfiguration;

class EventPipeEventsManager
{
public:
    EventPipeEventsManager(ICorProfilerInfo12* pCorProfilerInfo,
                           IAllocationsListener* pAllocationListener,
                           IContentionListener* pContentionListener,
                           IGCSuspensionsListener* pGCSuspensionsListener,
                           INetworkListener* pNetworkListener,
                           IConfiguration* pConfiguration,
                           IGCDumpListener* pGCDumpListener);
    void Register(IGarbageCollectionsListener* pGarbageCollectionsListener);
    void ParseEvent(EVENTPIPE_PROVIDER provider,
                    DWORD eventId,
                    DWORD eventVersion,
                    ULONG cbMetadataBlob,
                    LPCBYTE metadataBlob,
                    ULONG cbEventData,
                    LPCBYTE eventData,
                    LPCGUID pActivityId,
                    LPCGUID pRelatedActivityId,
                    ThreadID eventThread,
                    ULONG numStackFrames,
                    UINT_PTR stackFrames[]);

private:
    bool TryGetEventInfo(
        LPCBYTE pMetadata,
        ULONG cbMetadata,
        WCHAR*& name,
        DWORD& id,
        INT64& keywords,
        DWORD& version
        );

    // Resolves (and caches) the provider that emitted an event, to avoid calling
    // EventPipeGetProviderInfo (which copies the provider name) plus the string
    // comparisons on every event.
    DotnetEventsProvider GetProviderType(EVENTPIPE_PROVIDER provider);

public:
    // Called from EventPipeProviderCreated: a provider can be destroyed and a new one
    // allocated at the same address, so drop any stale cache entry for that address.
    void OnProviderCreated(EVENTPIPE_PROVIDER provider);


private:
    // We only ever subscribe to a handful of providers (<= 6). Cap the cache so it
    // stays bounded even if providers are re-created; if the cap is reached the cache
    // is dropped and entries are re-resolved lazily.
    static constexpr size_t MaxCachedProviders = 64;

    ICorProfilerInfo12* _pCorProfilerInfo;
    std::unique_ptr<ClrEventsParser> _clrParser;
    std::unique_ptr<BclEventsParser> _bclParser;
    // The event callback is not guaranteed to be single-threaded, so the cache is
    // guarded by a reader/writer lock: shared for lookups, exclusive to update.
    std::shared_mutex _providerTypesLock;
    std::unordered_map<EVENTPIPE_PROVIDER, DotnetEventsProvider> _providerTypes;
};
