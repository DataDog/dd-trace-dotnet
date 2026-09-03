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
    void OnProviderCreated(EVENTPIPE_PROVIDER provider);
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

    bool TryResolveProvider(EVENTPIPE_PROVIDER provider, DotnetEventsProvider& result);
    DotnetEventsProvider GetProvider(EVENTPIPE_PROVIDER provider);


private:
    ICorProfilerInfo12* _pCorProfilerInfo;
    std::unique_ptr<ClrEventsParser> _clrParser;
    std::unique_ptr<BclEventsParser> _bclParser;
    std::shared_mutex _providersMutex;
    std::unordered_map<EVENTPIPE_PROVIDER, DotnetEventsProvider> _providers;
};
