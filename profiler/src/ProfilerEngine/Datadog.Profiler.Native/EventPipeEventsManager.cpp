// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "EventPipeEventsManager.h"

#include "EventsParserHelper.h"
#include "IAllocationsListener.h"
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
    IGCDumpListener* pGCDumpListener)
    :
    _pCorProfilerInfo{pCorProfilerInfo}
{
    _clrParser = std::make_unique<ClrEventsParser>(
        pAllocationListener,
        pContentionListener,
        pGCSuspensionsListener,
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

    // Now that the BCL events are also received through EventPipe, it is needed to know which provider is sending each event.
    // It is possible to get the provider name from ICorProfilerInfo::EventPipeGetProviderInfo but the characters will
    // be copied each time an event is received: this could have a perf impact.
    // If this is the case, we could use the undocumented implementation details behind the EVENTPIPE_PROVIDER pointer
    // to the internal _EventPipeProvider structure from ep-provider.h:
    //    struct _EventPipeProvider {
    //        // Bit vector containing the currently enabled keywords.
    //        int64_t keywords;
    //        // Bit mask of sessions for which this provider is enabled.
    //        uint64_t sessions;
    //        // The name of the provider.
    //        ep_char8_t* provider_name;
    //        ep_char16_t* provider_name_utf16;
    // so the provider ANSI name is at offset 16 from the "provider" pointer
    ULONG nameLength = 256;
    WCHAR providerName[256];
    HRESULT hr = _pCorProfilerInfo->EventPipeGetProviderInfo(provider, nameLength, &nameLength, providerName);
    if (FAILED(hr))
    {
        return;
    }

    DotnetEventsProvider dotnetProvider = DotnetEventsProvider::Unknown;

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
