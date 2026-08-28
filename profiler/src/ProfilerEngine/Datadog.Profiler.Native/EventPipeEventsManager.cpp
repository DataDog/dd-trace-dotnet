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
    // Treat the provider-created notification as the authoritative writer of the cache entry:
    // always overwrite (including Unknown) so that a reused EVENTPIPE_PROVIDER address that now
    // belongs to a different provider replaces any stale entry.
    //
    // The resolve-then-insert sequence is done as a single critical section (instead of resolving
    // outside the lock) so that this is the only place calling into
    // ICorProfilerInfo::EventPipeGetProviderInfo: a provider can be reported as created concurrently
    // on more than one thread (or a provider could otherwise still be mid-registration on the CLR
    // side), and calling into that API for the same provider from more than one thread at once is
    // not something the profiling API is known to support safely.
    std::unique_lock lock(_providersMutex);
    _providers[provider] = ResolveProvider(provider);
}

DotnetEventsProvider EventPipeEventsManager::GetProvider(EVENTPIPE_PROVIDER provider)
{
    std::shared_lock lock(_providersMutex);
    auto it = _providers.find(provider);
    if (it != _providers.end())
    {
        return it->second;
    }

    // No EventPipeProviderCreated notification has been received (yet) for this provider.
    // Do NOT call into ICorProfilerInfo::EventPipeGetProviderInfo from here: this is the hot,
    // possibly-concurrent event-delivery path, and OnProviderCreated is expected to always
    // populate the cache before any event for a given provider is delivered. Treat an unresolved
    // provider as unknown and drop the event rather than resolving it out-of-band.
    return DotnetEventsProvider::Unknown;
}

DotnetEventsProvider EventPipeEventsManager::ResolveProvider(EVENTPIPE_PROVIDER provider)
{
    // Now that the BCL events are also received through EventPipe, it is needed to know which provider is sending each event.
    // It is possible to get the provider name from ICorProfilerInfo::EventPipeGetProviderInfo but the characters will
    // be copied each time it is called: this is why the result is cached per provider.
    ULONG nameLength = 256;
    WCHAR providerName[256];
    HRESULT hr = _pCorProfilerInfo->EventPipeGetProviderInfo(provider, nameLength, &nameLength, providerName);
    if (FAILED(hr))
    {
        return DotnetEventsProvider::Unknown;
    }

    // CLR events: "Microsoft-Windows-DotNETRuntime"
    if (WStrCmp(providerName, WStr("Microsoft-Windows-DotNETRuntime")) == 0)
    {
        return DotnetEventsProvider::Clr;
    }

    // BCL events: "System.Net.Http"
    //             "System.Net.Sockets"
    //             "System.Net.NameResolution"
    //             "System.Net.Security"
    if (WStrCmp(providerName, WStr("System.Net.Http")) == 0)
    {
        return DotnetEventsProvider::Http;
    }

    if (WStrCmp(providerName, WStr("System.Net.Sockets")) == 0)
    {
        return DotnetEventsProvider::Sockets;
    }

    if (WStrCmp(providerName, WStr("System.Net.NameResolution")) == 0)
    {
        return DotnetEventsProvider::NameResolution;
    }

    if (WStrCmp(providerName, WStr("System.Net.Security")) == 0)
    {
        return DotnetEventsProvider::NetSecurity;
    }

    return DotnetEventsProvider::Unknown;
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
