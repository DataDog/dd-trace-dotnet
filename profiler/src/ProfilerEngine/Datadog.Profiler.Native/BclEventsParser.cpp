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
    std::chrono::nanoseconds timestamp,
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
    // check only once
    if (_pNetworkListener == nullptr)
    {
        return;
    }

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
    std::chrono::nanoseconds timestamp,
    DWORD version,
    INT64 keywords,
    DWORD id,
    LPCBYTE pEventData,
    ULONG cbEventData,
    LPCGUID pActivityId,
    LPCGUID pRelatedActivityId
)
{
    // OnXXX methods not used are commented out to avoid unnecessary calls
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
            // OnConnectionEstablished(timestamp, pActivityId, pRelatedActivityId, pEventData, cbEventData);
            break;
        case 5: // ConnectionClosed
            // OnConnectionClosed(timestamp, pActivityId, pRelatedActivityId, pEventData, cbEventData);
            break;
        case 6: // RequestLeftQueue
            // OnRequestLeftQueue(timestamp, pActivityId, pRelatedActivityId, pEventData, cbEventData);
            break;
        case 7: // RequestHeadersStart
            OnRequestHeadersStart(timestamp, pActivityId, pRelatedActivityId, pEventData, cbEventData);
            break;
        case 8: // RequestHeadersStop
            // OnRequestHeadersStop(timestamp, pActivityId, pRelatedActivityId, pEventData, cbEventData);
            break;
        case 9: // RequestContentStart
            // OnRequestContentStart(timestamp, pActivityId, pRelatedActivityId);
            break;
        case 10: // RequestContentStop
            // OnRequestContentStop(timestamp, pActivityId, pRelatedActivityId, pEventData, cbEventData);
            break;
        case 11: // ResponseHeadersStart
            // OnResponseHeadersStart(timestamp, pActivityId, pRelatedActivityId, pEventData, cbEventData);
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
            // OnRequestFailedDetailed(timestamp, pActivityId, pRelatedActivityId, pEventData, cbEventData);
            break;
        case 16: // Redirect
            OnRedirect(timestamp, pActivityId, pRelatedActivityId, pEventData, cbEventData);
            break;
        default:
            break;
    }
}

void BclEventsParser::OnRequestStart(std::chrono::nanoseconds timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId, LPCBYTE pEventData, ULONG cbEventData)
{
    // string scheme
    // string host
    // int port
    // string path
    // ... more fields

    ULONG offset = 0;

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

    int port = 0;
    if (!EventsParserHelper::Read(port, pEventData, cbEventData, offset))
    {
        return;
    }

    WCHAR* path = EventsParserHelper::ReadWideString(pEventData, cbEventData, &offset);
    if (path == nullptr)
    {
        return;
    }

    std::string url = shared::ToString(shared::WSTRING(scheme)) + std::string("://") + shared::ToString(shared::WSTRING(host));
    if (port != 0)
    {
        url = url + ":" + std::to_string(port);
    }
    url = url + shared::ToString(shared::WSTRING(path));

    _pNetworkListener->OnRequestStart(timestamp, pActivityId, url);
}

void BclEventsParser::OnRequestStop(std::chrono::nanoseconds timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId, LPCBYTE pEventData, ULONG cbEventData)
{
    // payload:
    // int statusCode

    ULONG offset = 0;
    uint32_t statusCode = 0;
    if (!EventsParserHelper::Read(statusCode, pEventData, cbEventData, offset))
    {
        return;
    }

    _pNetworkListener->OnRequestStop(timestamp, pActivityId, statusCode);
}

void BclEventsParser::OnRequestFailed(std::chrono::nanoseconds timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId, LPCBYTE pEventData, ULONG cbEventData)
{
    // payload:
    // string exception message

    ULONG offset = 0;
    WCHAR* message = EventsParserHelper::ReadWideString(pEventData, cbEventData, &offset);
    if (message == nullptr)
    {
        return;
    }

    _pNetworkListener->OnRequestFailed(timestamp, pActivityId, shared::ToString(shared::WSTRING(message)));
}

void BclEventsParser::OnRequestFailedDetailed(std::chrono::nanoseconds timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId, LPCBYTE pEventData, ULONG cbEventData)
{
   // we don't need the exception callstack
}

void BclEventsParser::OnConnectionEstablished(std::chrono::nanoseconds timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId, LPCBYTE pEventData, ULONG cbEventData)
{
    // WARNING: the payload is different in .NET 7 and .NET 8+
    // .NET 7:
    //  ------
    //  byte versionMajor
    //  byte versionMinor
    //
    // .NET 8+
    //  ------
    //  long connectionId
    //  string scheme
    //  string host
    //  int port
    //  string remoteAddress
}

void BclEventsParser::OnConnectionClosed(std::chrono::nanoseconds timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId, LPCBYTE pEventData, ULONG cbEventData)
{
}

void BclEventsParser::OnRequestLeftQueue(std::chrono::nanoseconds timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId, LPCBYTE pEventData, ULONG cbEventData)
{
}

void BclEventsParser::OnRequestHeadersStart(std::chrono::nanoseconds timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId, LPCBYTE pEventData, ULONG cbEventData)
{
    // WARNING: no payload in .NET 7
    //
    // .NET 8+
    //  ------
    // Int64 connectionId = 0;

    _pNetworkListener->OnRequestHeaderStart(timestamp, pActivityId);
}

void BclEventsParser::OnRequestHeadersStop(std::chrono::nanoseconds timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId, LPCBYTE pEventData, ULONG cbEventData)
{
}

void BclEventsParser::OnRequestContentStart(std::chrono::nanoseconds timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId)
{
}

void BclEventsParser::OnRequestContentStop(std::chrono::nanoseconds timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId, LPCBYTE pEventData, ULONG cbEventData)
{
}

void BclEventsParser::OnResponseHeadersStart(std::chrono::nanoseconds timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId, LPCBYTE pEventData, ULONG cbEventData)
{
}

void BclEventsParser::OnResponseHeadersStop(std::chrono::nanoseconds timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId, LPCBYTE pEventData, ULONG cbEventData)
{
    // int statusCode

    ULONG offset = 0;
    uint32_t statusCode = 0;
    if (!EventsParserHelper::Read(statusCode, pEventData, cbEventData, offset))
    {
        return;
    }

    _pNetworkListener->OnResponseHeaderStop(timestamp, pActivityId, statusCode);
}

void BclEventsParser::OnResponseContentStart(std::chrono::nanoseconds timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId, LPCBYTE pEventData, ULONG cbEventData)
{
    _pNetworkListener->OnResponseContentStart(timestamp, pActivityId);
}

void BclEventsParser::OnResponseContentStop(std::chrono::nanoseconds timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId, LPCBYTE pEventData, ULONG cbEventData)
{
    _pNetworkListener->OnResponseContentStop(timestamp, pActivityId);
}

void BclEventsParser::OnRedirect(std::chrono::nanoseconds timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId, LPCBYTE pEventData, ULONG cbEventData)
{
    // string redirectUrl

    ULONG offset = 0;
    WCHAR* redirectUrl = EventsParserHelper::ReadWideString(pEventData, cbEventData, &offset);

    if (redirectUrl == nullptr)
    {
        return;
    }

    _pNetworkListener->OnRedirect(timestamp, pActivityId, shared::ToString(shared::WSTRING(redirectUrl)));
}


void BclEventsParser::ParseSocketsEvent(
    std::chrono::nanoseconds timestamp,
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
        case 1: // ConnectStart
            OnConnectStart(timestamp, pActivityId, pRelatedActivityId);
            break;
        case 2: // ConnectStop
            OnConnectStop(timestamp, pActivityId, pRelatedActivityId);
            break;
        case 3: // ConnectFailed
            OnConnectFailed(timestamp, pActivityId, pRelatedActivityId, pEventData, cbEventData);
            break;

        // the following events are related to incoming requests that we don't monitor
        //
        case 4: // AcceptStart
            break;
        case 5: // AcceptStop
            break;
        case 6: // AcceptFailed
            break;
        default:
            break;
    }
}

void BclEventsParser::OnConnectStart(std::chrono::nanoseconds timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId)
{
    _pNetworkListener->OnConnectStart(timestamp, pActivityId);
}

void BclEventsParser::OnConnectStop(std::chrono::nanoseconds timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId)
{
    _pNetworkListener->OnConnectStop(timestamp, pActivityId);
}

void BclEventsParser::OnConnectFailed(std::chrono::nanoseconds timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId, LPCBYTE pEventData, ULONG cbEventData)
{
    // string exception message

    ULONG offset = 0;
    WCHAR* message = EventsParserHelper::ReadWideString(pEventData, cbEventData, &offset);
    if (message == nullptr)
    {
        return;
    }

    _pNetworkListener->OnConnectFailed(timestamp, pActivityId, shared::ToString(message));
}


void BclEventsParser::ParseNameResolutionEvent(
    std::chrono::nanoseconds timestamp,
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
        case 1: // DnsResolutionStart
            OnDnsResolutionStart(timestamp, pActivityId, pRelatedActivityId, pEventData, cbEventData);
            break;
        case 2: // DnsResolutionStop
            OnDnsResolutionStop(timestamp, pActivityId, pRelatedActivityId, pEventData, cbEventData, true);
            break;
        case 3: // DnsResolutionFailed
            OnDnsResolutionStop(timestamp, pActivityId, pRelatedActivityId, pEventData, cbEventData, false);
            break;
        default:
            break;
    }
}

void BclEventsParser::OnDnsResolutionStart(std::chrono::nanoseconds timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId, LPCBYTE pEventData, ULONG cbEventData)
{
    _pNetworkListener->OnDnsResolutionStart(timestamp, pActivityId);
}

void BclEventsParser::OnDnsResolutionStop(std::chrono::nanoseconds timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId, LPCBYTE pEventData, ULONG cbEventData, bool success)
{
    _pNetworkListener->OnDnsResolutionStop(timestamp, pActivityId, success);
}


void BclEventsParser::ParseNetSecurityEvent(
    std::chrono::nanoseconds timestamp,
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
    case 1: // HandshakeStart
        OnHandshakeStart(timestamp, pActivityId, pRelatedActivityId, pEventData, cbEventData);
        break;
    case 2: // HandshakeStop
        OnHandshakeStop(timestamp, pActivityId, pRelatedActivityId, pEventData, cbEventData);
        break;
    case 3: // HandshakeFailed
        OnHandshakeFailed(timestamp, pActivityId, pRelatedActivityId, pEventData, cbEventData);
        break;
    default:
        break;
    }
}

void BclEventsParser::OnHandshakeStart(std::chrono::nanoseconds timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId, LPCBYTE pEventData, ULONG cbEventData)
{
    // bool IsServer
    // string targetHost

    ULONG offset = 0;
    uint32_t isServer = 0;
    if (!EventsParserHelper::Read(isServer, pEventData, cbEventData, offset))
    {
        return;
    }

    WCHAR* targetHost = EventsParserHelper::ReadWideString(pEventData, cbEventData, &offset);
    if (targetHost == nullptr)
    {
        return;
    }

    _pNetworkListener->OnHandshakeStart(timestamp, pActivityId, shared::ToString(shared::WSTRING(targetHost)));
}

void BclEventsParser::OnHandshakeStop(std::chrono::nanoseconds timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId, LPCBYTE pEventData, ULONG cbEventData)
{
    _pNetworkListener->OnHandshakeStop(timestamp, pActivityId);
}

void BclEventsParser::OnHandshakeFailed(std::chrono::nanoseconds timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId, LPCBYTE pEventData, ULONG cbEventData)
{
    // bool IsServer
    // double elapsedMilliseconds
    // string exceptionMessage

    ULONG offset = 0;
    uint32_t isServer = 0;
    if (!EventsParserHelper::Read(isServer, pEventData, cbEventData, offset))
    {
        return;
    }

    double elapsedMilliseconds = 0;
    if (!EventsParserHelper::Read(elapsedMilliseconds, pEventData, cbEventData, offset))
    {
        return;
    }

    WCHAR* message = EventsParserHelper::ReadWideString(pEventData, cbEventData, &offset);
    if (message == nullptr)
    {
        return;
    }

    _pNetworkListener->OnHandshakeFailed(timestamp, pActivityId, shared::ToString(shared::WSTRING(message)));
}
