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
    // The provider identity is resolved once (in OnProviderCreated) and cached, keyed by the
    // EVENTPIPE_PROVIDER pointer, so the hot event-delivery path avoids calling
    // EventPipeGetProviderInfo and comparing the provider name on every event.
    DotnetEventsProvider dotnetProvider = GetProvider(provider);
    if (dotnetProvider == DotnetEventsProvider::Unknown)
    {
        return;
    }

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

    // Also, during the test, a last (keyword=0 id=1 V1) event is sent from "Microsoft-DotNETCore-EventPipe"
    if (dotnetProvider == DotnetEventsProvider::Clr)
    {
        // The events are expected to be processed synchronously so the current time is used as timestamp
        _clrParser->ParseEvent(OpSysTools::GetHighPrecisionTimestamp(), version, keywords, id, cbEventData, eventData);
    }
    else
    {
        // The events are expected to be processed synchronously so the current time is used as timestamp
        _bclParser->ParseEvent(dotnetProvider, provider, OpSysTools::GetHighPrecisionTimestamp(), version, keywords, id, eventData, cbEventData, pActivityId, pRelatedActivityId, eventThread);
    }
}

void EventPipeEventsManager::OnProviderCreated(EVENTPIPE_PROVIDER provider)
{
    std::unique_lock lock(_providersMutex);

    DotnetEventsProvider resolved;
    if (TryResolveProvider(provider, resolved))
    {
        // Overwrite unconditionally (including with Unknown) so that a reused EVENTPIPE_PROVIDER
        // address that now belongs to a different provider replaces any stale entry.
        _providers[provider] = resolved;
    }
    // else: EventPipeGetProviderInfo API itself failed. Leave the cache untouched rather than caching
    // Unknown: caching a transient failure would misclassify this provider and drop every one of its
    // events for the rest of the process. GetProvider's fallback below gets another chance to resolve
    // it once real events start flowing.
}

DotnetEventsProvider EventPipeEventsManager::GetProvider(EVENTPIPE_PROVIDER provider)
{
    {
        std::shared_lock lock(_providersMutex);
        auto it = _providers.find(provider);
        if (it != _providers.end())
        {
            return it->second;
        }
    }

    std::unique_lock lock(_providersMutex);

    // Re-check under the exclusive lock: another thread may have resolved this provider while this
    // thread was waiting for the lock.
    auto it = _providers.find(provider);
    if (it != _providers.end())
    {
        return it->second;
    }

    DotnetEventsProvider resolved;
    if (!TryResolveProvider(provider, resolved))
    {
        // the call to the CLR failed so don't update the cache: the provider may be resolved later when events are received
        return DotnetEventsProvider::Unknown;
    }

    _providers[provider] = resolved;
    return resolved;
}

bool EventPipeEventsManager::TryResolveProvider(EVENTPIPE_PROVIDER provider, DotnetEventsProvider& result)
{
    // Now that the BCL events are also received through EventPipe, it is needed to know which provider is sending each event.
    // It is possible to get the provider name from ICorProfilerInfo::EventPipeGetProviderInfo but the characters will
    // be copied each time it is called: this is why the result is cached per provider.
    ULONG nameLength = 256;
    WCHAR providerName[256];
    HRESULT hr = _pCorProfilerInfo->EventPipeGetProviderInfo(provider, nameLength, &nameLength, providerName);
    if (FAILED(hr))
    {
        return false;
    }

    // CLR events: "Microsoft-Windows-DotNETRuntime"
    if (WStrCmp(providerName, WStr("Microsoft-Windows-DotNETRuntime")) == 0)
    {
        result = DotnetEventsProvider::Clr;
        return true;
    }

    // BCL events: "System.Net.Http"
    //             "System.Net.Sockets"
    //             "System.Net.NameResolution"
    //             "System.Net.Security"
    if (WStrCmp(providerName, WStr("System.Net.Http")) == 0)
    {
        result = DotnetEventsProvider::Http;
        return true;
    }

    if (WStrCmp(providerName, WStr("System.Net.Sockets")) == 0)
    {
        result = DotnetEventsProvider::Sockets;
        return true;
    }

    if (WStrCmp(providerName, WStr("System.Net.NameResolution")) == 0)
    {
        result = DotnetEventsProvider::NameResolution;
        return true;
    }

    if (WStrCmp(providerName, WStr("System.Net.Security")) == 0)
    {
        result = DotnetEventsProvider::NetSecurity;
        return true;
    }

    // Resolved successfully, but not a provider we track
    result = DotnetEventsProvider::Unknown;
    return true;
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
