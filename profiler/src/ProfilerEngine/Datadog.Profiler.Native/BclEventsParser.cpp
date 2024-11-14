// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "BclEventsParser.h"
#include "EventsParserHelper.h"

BclEventsParser::BclEventsParser(INetworkListener* pNetworkListener)
    :
    _pNetworkListener{ pNetworkListener }
{
}

void BclEventsParser::ParseEvent(
    DotnetEventsProvider dotnetProvider,
    EVENTPIPE_PROVIDER provider,
    uint64_t timestamp,
    DWORD version,
    INT64 keywords,
    DWORD id,
    LPCBYTE pEventData,
    ULONG cbEventData,
    LPCGUID pActivityId,
    LPCGUID pRelatedActivityId,
    ThreadID eventThread
)
{
    switch (dotnetProvider)
    {
    case DotnetEventsProvider::Http:
        ParseHttpEvent(timestamp, version, keywords, id, pEventData, cbEventData, pActivityId, pRelatedActivityId);
        break;
    case DotnetEventsProvider::Sockets:
        ParseSocketsEvent(timestamp, version, keywords, id, pEventData, cbEventData, pActivityId, pRelatedActivityId);
        break;
    case DotnetEventsProvider::NameResolution:
        ParseNameResolutionEvent(timestamp, version, keywords, id, pEventData, cbEventData, pActivityId, pRelatedActivityId);
        break;
    case DotnetEventsProvider::NetSecurity:
        ParseNetSecurityEvent(timestamp, version, keywords, id, pEventData, cbEventData, pActivityId, pRelatedActivityId);
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
    LPCBYTE pEventData,
    ULONG cbEventData,
    LPCGUID pActivityId,
    LPCGUID pRelatedActivityId
)
{
    switch (id)
    {
    case 1: // RequestStart
        OnRequestStart(timestamp, pActivityId, pRelatedActivityId, pEventData, cbEventData);
        break;
    case 2: // RequestStop
        OnRequestStop(timestamp, pActivityId, pRelatedActivityId, pEventData, cbEventData);
        break;
    case 3: // RequestFailed
        OnRequestFailed(timestamp, pActivityId, pRelatedActivityId, pEventData, cbEventData);
        break;
    case 4: // ConnectionEstablished
        OnConnectionEstablished(timestamp, pActivityId, pRelatedActivityId, pEventData, cbEventData);
        break;
    case 5: // ConnectionClosed
        OnConnectionClosed(timestamp, pActivityId, pRelatedActivityId, pEventData, cbEventData);
        break;
    case 6: // RequestLeftQueue
        OnRequestLeftQueue(timestamp, pActivityId, pRelatedActivityId, pEventData, cbEventData);
        break;
    case 7: // RequestHeadersStart
        OnRequestHeadersStart(timestamp, pActivityId, pRelatedActivityId, pEventData, cbEventData);
        break;
    case 8: // RequestHeadersStop
        OnRequestHeadersStop(timestamp, pActivityId, pRelatedActivityId, pEventData, cbEventData);
        break;
    case 9: // RequestContentStart
        OnRequestContentStart(timestamp, pActivityId, pRelatedActivityId);
        break;
    case 10: // RequestContentStop
        OnRequestContentStop(timestamp, pActivityId, pRelatedActivityId, pEventData, cbEventData);
        break;
    case 11: // ResponseHeadersStart
        OnResponseHeadersStart(timestamp, pActivityId, pRelatedActivityId, pEventData, cbEventData);
        break;
    case 12: // ResponseHeadersStop
        OnResponseHeadersStop(timestamp, pActivityId, pRelatedActivityId, pEventData, cbEventData);
        break;
    case 13: // ResponseContentStart
        OnResponseContentStart(timestamp, pActivityId, pRelatedActivityId, pEventData, cbEventData);
        break;
    case 14: // ResponseContentStop
        OnResponseContentStop(timestamp, pActivityId, pRelatedActivityId, pEventData, cbEventData);
        break;
    case 15: // RequestFailedDetailed
        OnRequestFailedDetailed(timestamp, pActivityId, pRelatedActivityId, pEventData, cbEventData);
        break;
    case 16: // Redirect
        OnRedirect(timestamp, pActivityId, pRelatedActivityId, pEventData, cbEventData);
        break;
    default:
        break;
    }
}

void BclEventsParser::OnRequestStart(uint64_t timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId, LPCBYTE pEventData, ULONG cbEventData)
{
    // string scheme
    // string host
    // int port
    // string path
    // ... more fields

    ULONG offset = 0;
    int port = 0;
    std::string url;

    WCHAR* scheme = EventsParserHelper::ReadWideString(pEventData, cbEventData, &offset);
    if (scheme == nullptr)
    {
        return;
    }

    WCHAR* host = EventsParserHelper::ReadWideString(pEventData, cbEventData, &offset);
    if (host == nullptr)
    {
        return;
    }

    if (!EventsParserHelper::Read(port, pEventData, cbEventData, offset))
    {
        return;
    }

    WCHAR* path = EventsParserHelper::ReadWideString(pEventData, cbEventData, &offset);
    if (path == nullptr)
    {
        return;
    }

    if (_pNetworkListener != nullptr)
    {
        std::string url = shared::ToString(shared::WSTRING(scheme)) + std::string("://") + shared::ToString(shared::WSTRING(host));
        if (port != 0)
        {
            url = url + ":" + std::to_string(port) + shared::ToString(shared::WSTRING(path));
        }
        else
        {
            url = url + shared::ToString(shared::WSTRING(path));
        }
        _pNetworkListener->OnRequestStart(timestamp, pActivityId, url);
    }
}
void BclEventsParser::OnRequestStop(uint64_t timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId, LPCBYTE pEventData, ULONG cbEventData)
{
    // int statusCode

    ULONG offset = 0;
    uint32_t statusCode = 0;
    if (!EventsParserHelper::Read(statusCode, pEventData, cbEventData, offset))
    {
        return;
    }

    if (_pNetworkListener != nullptr)
    {
        _pNetworkListener->OnRequestStop(timestamp, pActivityId, statusCode);
    }
}

void BclEventsParser::OnRequestFailed(uint64_t timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId, LPCBYTE pEventData, ULONG cbEventData)
{
    // string exception message

    ULONG offset = 0;
    WCHAR* message = EventsParserHelper::ReadWideString(pEventData, cbEventData, &offset);
    if (message == nullptr)
    {
        return;
    }

    if (_pNetworkListener != nullptr)
    {
        _pNetworkListener->OnRequestFailed(timestamp, pActivityId, shared::ToString(shared::WSTRING(message)));
    }
}

void BclEventsParser::OnRequestFailedDetailed(uint64_t timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId, LPCBYTE pEventData, ULONG cbEventData)
{
}

void BclEventsParser::OnConnectionEstablished(uint64_t timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId, LPCBYTE pEventData, ULONG cbEventData)
{
}

void BclEventsParser::OnConnectionClosed(uint64_t timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId, LPCBYTE pEventData, ULONG cbEventData)
{
}

void BclEventsParser::OnRequestLeftQueue(uint64_t timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId, LPCBYTE pEventData, ULONG cbEventData)
{
}

void BclEventsParser::OnRequestHeadersStart(uint64_t timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId, LPCBYTE pEventData, ULONG cbEventData)
{
}

void BclEventsParser::OnRequestHeadersStop(uint64_t timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId, LPCBYTE pEventData, ULONG cbEventData)
{
}

void BclEventsParser::OnRequestContentStart(uint64_t timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId)
{
}

void BclEventsParser::OnRequestContentStop(uint64_t timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId, LPCBYTE pEventData, ULONG cbEventData)
{
}

void BclEventsParser::OnResponseHeadersStart(uint64_t timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId, LPCBYTE pEventData, ULONG cbEventData)
{
}

void BclEventsParser::OnResponseHeadersStop(uint64_t timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId, LPCBYTE pEventData, ULONG cbEventData)
{
}

void BclEventsParser::OnResponseContentStart(uint64_t timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId, LPCBYTE pEventData, ULONG cbEventData)
{
}

void BclEventsParser::OnResponseContentStop(uint64_t timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId, LPCBYTE pEventData, ULONG cbEventData)
{
}

void BclEventsParser::OnRedirect(uint64_t timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId, LPCBYTE pEventData, ULONG cbEventData)
{
}



void BclEventsParser::ParseSocketsEvent(
    uint64_t timestamp,
    DWORD version,
    INT64 keywords,
    DWORD id,
    LPCBYTE pEventData,
    ULONG cbEventData,
    LPCGUID pActivityId,
    LPCGUID pRelatedActivityId
)
{
    // Method implementation goes here
}

void BclEventsParser::ParseNameResolutionEvent(
    uint64_t timestamp,
    DWORD version,
    INT64 keywords,
    DWORD id,
    LPCBYTE pEventData,
    ULONG cbEventData,
    LPCGUID pActivityId,
    LPCGUID pRelatedActivityId
)
{
    // Method implementation goes here
}

void BclEventsParser::ParseNetSecurityEvent(
    uint64_t timestamp,
    DWORD version,
    INT64 keywords,
    DWORD id,
    LPCBYTE pEventData,
    ULONG cbEventData,
    LPCGUID pActivityId,
    LPCGUID pRelatedActivityId
)
{
    // Method implementation goes here
}