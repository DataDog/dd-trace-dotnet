// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

// from dotnet coreclr includes
#include "cor.h"
#include "corprof.h"
// end

#include <memory>

#include "ClrEventsParser.h"


class IAllocationsListener;
class IContentionListener;
class IGCSuspensionsListener;
class IGarbageCollectionsListener;


class EventPipeEventsManager
{
public:
    EventPipeEventsManager(IAllocationsListener* pAllocationListener,
                           IContentionListener* pContentionListener,
                           IGCSuspensionsListener* pGCSuspensionsListener);
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


private:
    std::unique_ptr<ClrEventsParser> _parser;
};
