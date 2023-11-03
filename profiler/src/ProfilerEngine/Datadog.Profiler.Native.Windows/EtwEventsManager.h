// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <memory>

#include "IEtwEventsManager.h"
#include "ClrEventsParser.h"

class EtwEventsManager : public IEtwEventsManager
{
public:
    EtwEventsManager(
        IAllocationsListener* pAllocationListener,
        IContentionListener* pContentionListener,
        IGCSuspensionsListener* pGCSuspensionsListener
        );

    virtual void Register(IGarbageCollectionsListener* pGarbageCollectionsListener) override;
    virtual bool Start() override;
    virtual void Stop() override;

private:
    std::unique_ptr<ClrEventsParser> _parser;
};
