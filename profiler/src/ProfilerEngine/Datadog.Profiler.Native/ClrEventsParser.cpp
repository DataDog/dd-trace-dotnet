// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "ClrEventsParser.h"

#include <iostream>
#include <sstream>
#include <iomanip>

#include "IAllocationsListener.h"
#include "Log.h"


ClrEventsParser::ClrEventsParser(ICorProfilerInfo12* pCorProfilerInfo, IAllocationsListener* pAllocationListener)
    :
    _pCorProfilerInfo{pCorProfilerInfo},
    _pAllocationListener{pAllocationListener}
{
}

void ClrEventsParser::ParseEvent(
    EVENTPIPE_PROVIDER provider,
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
    // However, during the test, a last (k=0 id=1 V1) event is sent from "Microsoft-DotNETCore-EventPipe".

    // These should be the same as eventId and eventVersion.
    // However it was not the case for the last event received from "Microsoft-DotNETCore-EventPipe".
    DWORD id;
    DWORD version;
    INT64 keywords;  // used to filter out unneeded events.
    WCHAR* name;
    if (!TryGetEventInfo(metadataBlob, cbMetadataBlob, name, id, keywords, version))
    {
        return;
    }

    if (KEYWORD_GC == (keywords & KEYWORD_GC))
    {
        ParseGcEvent(id, version, cbEventData, eventData);
    }
    else
    if (KEYWORD_CONTENTION == (keywords & KEYWORD_CONTENTION))
    {
        ParseContentionEvent(id, version, cbEventData, eventData);
    }
}

void ClrEventsParser::ParseGcEvent(DWORD id, DWORD version, ULONG cbEventData, LPCBYTE pEventData)
{
// look for AllocationTick_V4
    if ((id == EVENT_ALLOCATION_TICK) && (version == 4))
    {
        //template tid = "GCAllocationTick_V4" >
        //    <data name = "AllocationAmount" inType = "win:UInt32" />
        //    <data name = "AllocationKind" inType = "win:UInt32" />
        //    <data name = "ClrInstanceID" inType = "win:UInt16" />
        //    <data name = "AllocationAmount64" inType = "win:UInt64"/>
        //    <data name = "TypeID" inType = "win:Pointer" />
        //    <data name = "TypeName" inType = "win:UnicodeString" />
        //    <data name = "HeapIndex" inType = "win:UInt32" />
        //    <data name = "Address" inType = "win:Pointer" />
        //    <data name = "ObjectSize" inType = "win:UInt64" />
        //DumpBuffer(pEventData, cbEventData);

        AllocationTickV4Payload payload{0};
        ULONG offset = 0;
        if (!Read(payload.AllocationAmount, pEventData, cbEventData, offset))
        {
            return;
        }
        if (!Read(payload.AllocationKind, pEventData, cbEventData, offset))
        {
            return;
        }
        if (!Read(payload.ClrInstanceId, pEventData, cbEventData, offset))
        {
            return;
        }
        if (!Read(payload.AllocationAmount64, pEventData, cbEventData, offset))
        {
            return;
        }
        if (!Read(payload.TypeId, pEventData, cbEventData, offset))
        {
            return;
        }
        payload.TypeName = ReadWideString(pEventData, cbEventData, &offset);
        if (payload.TypeName == nullptr)
        {
            return;
        }
        if (!Read(payload.HeapIndex, pEventData, cbEventData, offset))
        {
            return;
        }
        if (!Read(payload.Address, pEventData, cbEventData, offset))
        {
            return;
        }
        if (!Read(payload.ObjectSize, pEventData, cbEventData, offset))
        {
            return;
        }

        _pAllocationListener->OnAllocation(payload.AllocationKind, payload.TypeName, payload.Address, payload.ObjectSize);
    }
}

void ClrEventsParser::ParseContentionEvent(DWORD id, DWORD version, ULONG cbEventData, LPCBYTE pEventData)
{
// look for ContentionStop_V1
    if ((id == EVENT_CONTENTION_STOP) && (version == 1))
    {
        //<template tid="ContentionStop_V1">
        //    <data name="ContentionFlags" inType="win:UInt8" />
        //    <data name="ClrInstanceID" inType="win:UInt16" />
        //    <data name="DurationNs" inType="win:Double" />
        //DumpBuffer(pEventData, cbEventData);

        ContentionStopV1Payload payload{0};
        ULONG offset = 0;
        if (!Read<ContentionStopV1Payload>(payload, pEventData, cbEventData, offset))
        {
            return;
        }
    }
}

bool ClrEventsParser::TryGetEventInfo(LPCBYTE pMetadata, ULONG cbMetadata, WCHAR*& name, DWORD& id, INT64& keywords, DWORD& version)
{
    if (pMetadata == NULL || cbMetadata == 0)
    {
        return false;
    }

    ULONG offset = 0;
    Read(id, pMetadata, cbMetadata, offset);

    // skip the name to read keyword and version
    name = ReadWideString(pMetadata, cbMetadata, &offset);
    Read(keywords, pMetadata, cbMetadata, offset);
    Read(version, pMetadata, cbMetadata, offset);

    return true;
}