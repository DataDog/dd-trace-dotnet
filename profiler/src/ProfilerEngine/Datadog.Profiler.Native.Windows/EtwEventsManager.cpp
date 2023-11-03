// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.


#include "EtwEventsManager.h"

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

void EtwEventsManager::Register(IGarbageCollectionsListener* pGarbageCollectionsListener)
{
    _parser->Register(pGarbageCollectionsListener);
}

bool EtwEventsManager::Start()
{
    return true;
}

void EtwEventsManager::Stop()
{
}