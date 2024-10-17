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
        ULONG cbEventData,
        LPCBYTE eventData,
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
        ULONG cbEventData,
        LPCBYTE eventData,
        LPCGUID pActivityId,
        LPCGUID pRelatedActivityId,
        ThreadID eventThread
    );

    void ParseSocketsEvent(
        uint64_t timestamp,
        DWORD version,
        INT64 keywords,
        DWORD id,
        ULONG cbEventData,
        LPCBYTE eventData,
        LPCGUID pActivityId,
        LPCGUID pRelatedActivityId,
        ThreadID eventThread
    );

    void ParseNameResolutionEvent(
        uint64_t timestamp,
        DWORD version,
        INT64 keywords,
        DWORD id,
        ULONG cbEventData,
        LPCBYTE eventData,
        LPCGUID pActivityId,
        LPCGUID pRelatedActivityId,
        ThreadID eventThread
    );

    void ParseNetSecurityEvent(
        uint64_t timestamp,
        DWORD version,
        INT64 keywords,
        DWORD id,
        ULONG cbEventData,
        LPCBYTE eventData,
        LPCGUID pActivityId,
        LPCGUID pRelatedActivityId,
        ThreadID eventThread
    );

private:
    INetworkListener* _pNetworkListener;
};

