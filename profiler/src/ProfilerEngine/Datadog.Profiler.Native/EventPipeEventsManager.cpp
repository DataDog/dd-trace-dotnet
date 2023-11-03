// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "EventPipeEventsManager.h"

#include "IAllocationsListener.h"
#include "IContentionListener.h"
#include "IGCSuspensionsListener.h"

EventPipeEventsManager::EventPipeEventsManager(
    IAllocationsListener* pAllocationListener,
    IContentionListener* pContentionListener,
    IGCSuspensionsListener* pGCSuspensionsListener)
{
    _parser = std::make_unique<ClrEventsParser>(
        pAllocationListener,
        pContentionListener,
        pGCSuspensionsListener);
}

void EventPipeEventsManager::Register(IGarbageCollectionsListener* pGarbageCollectionsListener)
{
    _parser->Register(pGarbageCollectionsListener);
}

void EventPipeEventsManager::ParseEvent(EVENTPIPE_PROVIDER provider,
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
                UINT_PTR stackFrames[])
{
    // Currently, only "Microsoft-Windows-DotNETRuntime" provider is used so no need to check.
    // However, during the test, a last (keyword=0 id=1 V1) event is sent from "Microsoft-DotNETCore-EventPipe".

    // These should be the same as eventId and eventVersion.
    // However it was not the case for the last event received from "Microsoft-DotNETCore-EventPipe".
    DWORD id;
    DWORD version;
    INT64 keywords; // used to filter out unneeded events.
    WCHAR* name;
    if (!TryGetEventInfo(metadataBlob, cbMetadataBlob, name, id, keywords, version))
    {
        return;
    }

    _parser->ParseEvent(version, keywords, id, cbEventData, eventData);
}

bool EventPipeEventsManager::TryGetEventInfo(LPCBYTE pMetadata, ULONG cbMetadata, WCHAR*& name, DWORD& id, INT64& keywords, DWORD& version)
{
    if (pMetadata == nullptr || cbMetadata == 0)
    {
        return false;
    }

    ULONG offset = 0;
    if (!ClrEventsParser::Read(id, pMetadata, cbMetadata, offset))
    {
        return false;
    }

    // skip the name to read keyword and version
    name = ClrEventsParser::ReadWideString(pMetadata, cbMetadata, &offset);

    if (!ClrEventsParser::Read(keywords, pMetadata, cbMetadata, offset))
    {
        return false;
    }

    if (!ClrEventsParser::Read(version, pMetadata, cbMetadata, offset))
    {
        return false;
    }

    return true;
}
