// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <chrono>

#include "DotnetEventsProvider.h"
#include "INetworkListener.h"

// from dotnet coreclr includes
#include "cor.h"
#include "corprof.h"
// end


class BclEventsParser
{
public:
    BclEventsParser(INetworkListener* pNetworkListener);
    void ParseEvent(
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
    );

private:
    void ParseHttpEvent(
        std::chrono::nanoseconds timestamp,
        DWORD version,
        INT64 keywords,
        DWORD id,
        LPCBYTE pEventData,
        ULONG cbEventData,
        LPCGUID pActivityId,
        LPCGUID pRelatedActivityId
    );
    void OnRequestStart(std::chrono::nanoseconds timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId, LPCBYTE eventData, ULONG cbEventData);
    void OnRequestStop(std::chrono::nanoseconds timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId, LPCBYTE eventData, ULONG cbEventData);
    void OnRequestFailed(std::chrono::nanoseconds timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId, LPCBYTE eventData, ULONG cbEventData);
    void OnConnectionEstablished(std::chrono::nanoseconds timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId, LPCBYTE eventData, ULONG cbEventData);
    void OnConnectionClosed(std::chrono::nanoseconds timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId, LPCBYTE eventData, ULONG cbEventData);
    void OnRequestLeftQueue(std::chrono::nanoseconds timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId, LPCBYTE eventData, ULONG cbEventData);
    void OnRequestHeadersStart(std::chrono::nanoseconds timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId, LPCBYTE eventData, ULONG cbEventData);
    void OnRequestHeadersStop(std::chrono::nanoseconds timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId, LPCBYTE eventData, ULONG cbEventData);
    void OnRequestContentStart(std::chrono::nanoseconds timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId);
    void OnRequestContentStop(std::chrono::nanoseconds timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId, LPCBYTE eventData, ULONG cbEventData);
    void OnResponseHeadersStart(std::chrono::nanoseconds timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId, LPCBYTE eventData, ULONG cbEventData);
    void OnResponseHeadersStop(std::chrono::nanoseconds timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId, LPCBYTE eventData, ULONG cbEventData);
    void OnResponseContentStart(std::chrono::nanoseconds timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId, LPCBYTE eventData, ULONG cbEventData);
    void OnResponseContentStop(std::chrono::nanoseconds timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId, LPCBYTE eventData, ULONG cbEventData);
    void OnRequestFailedDetailed(std::chrono::nanoseconds timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId, LPCBYTE eventData, ULONG cbEventData);
    void OnRedirect(std::chrono::nanoseconds timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId, LPCBYTE eventData, ULONG cbEventData);


    void ParseSocketsEvent(
        std::chrono::nanoseconds timestamp,
        DWORD version,
        INT64 keywords,
        DWORD id,
        LPCBYTE pEventData,
        ULONG cbEventData,
        LPCGUID pActivityId,
        LPCGUID pRelatedActivityId
    );
    void OnConnectStart(std::chrono::nanoseconds timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId);
    void OnConnectStop(std::chrono::nanoseconds timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId);
    void OnConnectFailed(std::chrono::nanoseconds timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId, LPCBYTE eventData, ULONG cbEventData);

    void ParseNameResolutionEvent(
        std::chrono::nanoseconds timestamp,
        DWORD version,
        INT64 keywords,
        DWORD id,
        LPCBYTE pEventData,
        ULONG cbEventData,
        LPCGUID pActivityId,
        LPCGUID pRelatedActivityId
    );
    void OnDnsResolutionStart(std::chrono::nanoseconds timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId, LPCBYTE eventData, ULONG cbEventData);
    void OnDnsResolutionStop(std::chrono::nanoseconds timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId, LPCBYTE eventData, ULONG cbEventData, bool success);

    void ParseNetSecurityEvent(
        std::chrono::nanoseconds timestamp,
        DWORD version,
        INT64 keywords,
        DWORD id,
        LPCBYTE pEventData,
        ULONG cbEventData,
        LPCGUID pActivityId,
        LPCGUID pRelatedActivityId
    );

private:
    INetworkListener* _pNetworkListener;
};