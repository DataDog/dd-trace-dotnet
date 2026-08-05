// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "EventPipeEventsManager.h"

#include "EventsParserHelper.h"
#include "IAllocationsListener.h"
#include "IConfiguration.h"
#include "IContentionListener.h"
#include "IGCDumpListener.h"
#include "IGCSuspensionsListener.h"
#include "INetworkListener.h"
#include "OpSysTools.h"


EventPipeEventsManager::EventPipeEventsManager(
    ICorProfilerInfo12* pCorProfilerInfo,
    IAllocationsListener* pAllocationListener,
    IContentionListener* pContentionListener,
    IGCSuspensionsListener* pGCSuspensionsListener,
    INetworkListener* pNetworkListener,
    IConfiguration* pConfiguration,
    IGCDumpListener* pGCDumpListener)
    :
    _pCorProfilerInfo{pCorProfilerInfo}
{
    _clrParser = std::make_unique<ClrEventsParser>(
        pAllocationListener,
        pContentionListener,
        pGCSuspensionsListener,
        pConfiguration,
        pGCDumpListener);
    _bclParser = std::make_unique<BclEventsParser>(pNetworkListener);
}

void EventPipeEventsManager::Register(IGarbageCollectionsListener* pGarbageCollectionsListener)
{
    _clrParser->Register(pGarbageCollectionsListener);
}

void EventPipeEventsManager::ParseEvent(
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

    // Identify which provider emitted the event. Resolving the name (a copy plus string
    // comparisons) on every event was costly, so the result is cached per provider.
    DotnetEventsProvider dotnetProvider = GetProviderType(provider);

    // Also, during the test, a last (keyword=0 id=1 V1) event is sent from "Microsoft-DotNETCore-EventPipe"
    if (dotnetProvider == DotnetEventsProvider::Clr)
    {
        // The events are expected to be processed synchronously so the current time is used as timestamp
        _clrParser->ParseEvent(OpSysTools::GetHighPrecisionTimestamp(), version, keywords, id, cbEventData, eventData);
    }
    else
    if (dotnetProvider != DotnetEventsProvider::Unknown)
    {
        // The events are expected to be processed synchronously so the current time is used as timestamp
        _bclParser->ParseEvent(dotnetProvider, provider, OpSysTools::GetHighPrecisionTimestamp(), version, keywords, id, eventData, cbEventData, pActivityId, pRelatedActivityId, eventThread);
    }
}

DotnetEventsProvider EventPipeEventsManager::GetProviderType(EVENTPIPE_PROVIDER provider)
{
    {
        std::shared_lock<std::shared_mutex> readLock(_providerTypesLock);
        auto found = _providerTypes.find(provider);
        if (found != _providerTypes.end())
        {
            return found->second;
        }
    }

    // Cache miss: resolve the name once. It is possible to get the provider name from
    // ICorProfilerInfo::EventPipeGetProviderInfo but the characters are copied on each
    // call, so we only do it here.
    DotnetEventsProvider dotnetProvider = DotnetEventsProvider::Unknown;

    ULONG nameLength = 256;
    WCHAR providerName[256];
    HRESULT hr = _pCorProfilerInfo->EventPipeGetProviderInfo(provider, nameLength, &nameLength, providerName);
    if (FAILED(hr))
    {
        // Do not cache a failed lookup: the provider pointer may become resolvable later.
        return DotnetEventsProvider::Unknown;
    }

    // CLR events: "Microsoft-Windows-DotNETRuntime"
    if (WStrCmp(providerName, WStr("Microsoft-Windows-DotNETRuntime")) == 0)
    {
        dotnetProvider = DotnetEventsProvider::Clr;
    }
    else
    // BCL events: "System.Net.Http"
    //             "System.Net.Sockets"
    //             "System.Net.NameResolution"
    //             "System.Net.Security"
    if (WStrCmp(providerName, WStr("System.Net.Http")) == 0)
    {
        dotnetProvider = DotnetEventsProvider::Http;
    }
    else
    if (WStrCmp(providerName, WStr("System.Net.Sockets")) == 0)
    {
        dotnetProvider = DotnetEventsProvider::Sockets;
    }
    else
    if (WStrCmp(providerName, WStr("System.Net.NameResolution")) == 0)
    {
        dotnetProvider = DotnetEventsProvider::NameResolution;
    }
    else
    if (WStrCmp(providerName, WStr("System.Net.Security")) == 0)
    {
        dotnetProvider = DotnetEventsProvider::NetSecurity;
    }

    std::unique_lock<std::shared_mutex> writeLock(_providerTypesLock);
    // Keep the cache bounded; drop it wholesale if we ever exceed the small cap.
    if (_providerTypes.size() >= MaxCachedProviders)
    {
        _providerTypes.clear();
    }

    _providerTypes[provider] = dotnetProvider;
    return dotnetProvider;
}

void EventPipeEventsManager::OnProviderCreated(EVENTPIPE_PROVIDER provider)
{
    // Avoid the exclusive lock unless this address is actually cached.
    {
        std::shared_lock<std::shared_mutex> readLock(_providerTypesLock);
        if (_providerTypes.find(provider) == _providerTypes.end())
        {
            return;
        }
    }

    std::unique_lock<std::shared_mutex> writeLock(_providerTypesLock);
    _providerTypes.erase(provider);
}

bool EventPipeEventsManager::TryGetEventInfo(LPCBYTE pMetadata, ULONG cbMetadata, WCHAR*& name, DWORD& id, INT64& keywords, DWORD& version)
{
    if (pMetadata == nullptr || cbMetadata == 0)
    {
        return false;
    }

    ULONG offset = 0;
    if (!EventsParserHelper::Read(id, pMetadata, cbMetadata, offset))
    {
        return false;
    }

    // skip the name to read keyword and version
    name = EventsParserHelper::ReadWideString(pMetadata, cbMetadata, &offset);
    if (name == nullptr)
    {
        return false;
    }

    if (!EventsParserHelper::Read(keywords, pMetadata, cbMetadata, offset))
    {
        return false;
    }

    if (!EventsParserHelper::Read(version, pMetadata, cbMetadata, offset))
    {
        return false;
    }

    return true;
}
