// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

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
        uint64_t timestamp,
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
        uint64_t timestamp,
        DWORD version,
        INT64 keywords,
        DWORD id,
        LPCBYTE pEventData,
        ULONG cbEventData,
        LPCGUID pActivityId,
        LPCGUID pRelatedActivityId
    );
    void OnRequestStart(uint64_t timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId, LPCBYTE eventData, ULONG cbEventData);
    void OnRequestStop(uint64_t timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId, LPCBYTE eventData, ULONG cbEventData);
    void OnRequestFailed(uint64_t timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId, LPCBYTE eventData, ULONG cbEventData);
    void OnConnectionEstablished(uint64_t timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId, LPCBYTE eventData, ULONG cbEventData);
    void OnConnectionClosed(uint64_t timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId, LPCBYTE eventData, ULONG cbEventData);
    void OnRequestLeftQueue(uint64_t timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId, LPCBYTE eventData, ULONG cbEventData);
    void OnRequestHeadersStart(uint64_t timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId, LPCBYTE eventData, ULONG cbEventData);
    void OnRequestHeadersStop(uint64_t timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId, LPCBYTE eventData, ULONG cbEventData);
    void OnRequestContentStart(uint64_t timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId);
    void OnRequestContentStop(uint64_t timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId, LPCBYTE eventData, ULONG cbEventData);
    void OnResponseHeadersStart(uint64_t timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId, LPCBYTE eventData, ULONG cbEventData);
    void OnResponseHeadersStop(uint64_t timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId, LPCBYTE eventData, ULONG cbEventData);
    void OnResponseContentStart(uint64_t timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId, LPCBYTE eventData, ULONG cbEventData);
    void OnResponseContentStop(uint64_t timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId, LPCBYTE eventData, ULONG cbEventData);
    void OnRequestFailedDetailed(uint64_t timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId, LPCBYTE eventData, ULONG cbEventData);
    void OnRedirect(uint64_t timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId, LPCBYTE eventData, ULONG cbEventData);


    void ParseSocketsEvent(
        uint64_t timestamp,
        DWORD version,
        INT64 keywords,
        DWORD id,
        LPCBYTE pEventData,
        ULONG cbEventData,
        LPCGUID pActivityId,
        LPCGUID pRelatedActivityId
    );
    void OnConnectStart(uint64_t timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId);
    void OnConnectStop(uint64_t timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId);
    void OnConnectFailed(uint64_t timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId, LPCBYTE eventData, ULONG cbEventData);

    void ParseNameResolutionEvent(
        uint64_t timestamp,
        DWORD version,
        INT64 keywords,
        DWORD id,
        LPCBYTE pEventData,
        ULONG cbEventData,
        LPCGUID pActivityId,
        LPCGUID pRelatedActivityId
    );
    void OnDnsResolutionStart(uint64_t timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId, LPCBYTE eventData, ULONG cbEventData);
    void OnDnsResolutionStop(uint64_t timestamp, LPCGUID pActivityId, LPCGUID pRelatedActivityId, LPCBYTE eventData, ULONG cbEventData, bool success);

    void ParseNetSecurityEvent(
        uint64_t timestamp,
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