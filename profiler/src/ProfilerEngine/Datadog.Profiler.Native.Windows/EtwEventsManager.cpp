// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.


#include "EtwEventsManager.h"

#include "Windows.h"

#include <string>

const std::string NamedPipePrefix = "\\\\.\\pipe\\DD_ETW_CLIENT_";
const std::string NamedPipeAgent = "\\\\.\\pipe\\DD_ETW_DISPATCHER";


EtwEventsManager::EtwEventsManager(
    IAllocationsListener* pAllocationListener,
    IContentionListener* pContentionListener,
    IGCSuspensionsListener* pGCSuspensionsListener)
{
    _parser = std::make_unique<ClrEventsParser>(
        pAllocationListener,
        pContentionListener,
        pGCSuspensionsListener);
}

void EtwEventsManager::OnEvent(
    uint32_t tid,
    uint32_t version,
    uint64_t keyword,
    uint8_t level,
    uint32_t id,
    uint32_t cbEventData,
    const uint8_t* pEventData)
{
    _parser.get()->ParseEvent(version, keyword, id, cbEventData, pEventData);
}

void EtwEventsManager::OnStop()
{
    // TODO: add some logs
}


void EtwEventsManager::Register(IGarbageCollectionsListener* pGarbageCollectionsListener)
{
    _parser->Register(pGarbageCollectionsListener);
}


bool EtwEventsManager::Start()
{
    DWORD pid = ::GetCurrentProcessId();

    // TODO: start the server named pipe

    // TODO: contact the Agent named pipe

    return true;
}

void EtwEventsManager::Stop()
{
}