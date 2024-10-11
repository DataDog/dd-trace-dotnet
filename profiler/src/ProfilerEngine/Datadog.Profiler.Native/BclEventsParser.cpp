// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "BclEventsParser.h"

BclEventsParser::BclEventsParser(INetworkListener* pNetworkListener)
    :
    _pNetworkListener{pNetworkListener}
{
}

void BclEventsParser::ParseEvent(
    DotnetEventsProvider dotnetProvider,
    EVENTPIPE_PROVIDER provider,
    uint64_t timestamp,
    DWORD version,
    INT64 keywords,
    DWORD id,
    ULONG cbEventData,
    LPCBYTE eventData,
    LPCGUID pActivityId,
    LPCGUID pRelatedActivityId,
    ThreadID eventThread
    )
{
    switch(dotnetProvider)
    {
        case DotnetEventsProvider::Http:
            ParseHttpEvent(timestamp, version, keywords, id, cbEventData, eventData, pActivityId, pRelatedActivityId, eventThread);
            break;
        case DotnetEventsProvider::Sockets:
            ParseSocketsEvent(timestamp, version, keywords, id, cbEventData, eventData, pActivityId, pRelatedActivityId, eventThread);
            break;
        case DotnetEventsProvider::NameResolution:
            ParseNameResolutionEvent(timestamp, version, keywords, id, cbEventData, eventData, pActivityId, pRelatedActivityId, eventThread);
            break;
        case DotnetEventsProvider::NetSecurity:
            ParseNetSecurityEvent(timestamp, version, keywords, id, cbEventData, eventData, pActivityId, pRelatedActivityId, eventThread);
            break;
        default:
            break;
   }
}

void BclEventsParser::ParseHttpEvent(
    uint64_t timestamp,
    DWORD version,
    INT64 keywords,
    DWORD id,
    ULONG cbEventData,
    LPCBYTE eventData,
    LPCGUID pActivityId,
    LPCGUID pRelatedActivityId,
    ThreadID eventThread
)
{
    // Method implementation goes here
}

void BclEventsParser::ParseSocketsEvent(
    uint64_t timestamp,
    DWORD version,
    INT64 keywords,
    DWORD id,
    ULONG cbEventData,
    LPCBYTE eventData,
    LPCGUID pActivityId,
    LPCGUID pRelatedActivityId,
    ThreadID eventThread
)
{
    // Method implementation goes here
}

void BclEventsParser::ParseNameResolutionEvent(
    uint64_t timestamp,
    DWORD version,
    INT64 keywords,
    DWORD id,
    ULONG cbEventData,
    LPCBYTE eventData,
    LPCGUID pActivityId,
    LPCGUID pRelatedActivityId,
    ThreadID eventThread
)
{
    // Method implementation goes here
}

void BclEventsParser::ParseNetSecurityEvent(
    uint64_t timestamp,
    DWORD version,
    INT64 keywords,
    DWORD id,
    ULONG cbEventData,
    LPCBYTE eventData,
    LPCGUID pActivityId,
    LPCGUID pRelatedActivityId,
    ThreadID eventThread
)
{
    // Method implementation goes here
}
