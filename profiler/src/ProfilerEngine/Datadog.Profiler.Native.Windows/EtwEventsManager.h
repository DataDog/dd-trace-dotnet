// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "IEtwEventsManager.h"
#include "..\Datadog.Profiler.Native\IClrEventsReceiver.h"
#include "ClrEventsParser.h"
#include "ETW/IpcClient.h"
#include "ETW/IpcServer.h"
#include "ETW/EtwEventsHandler.h"

#include <memory>


class EtwEventsManager :
    public IEtwEventsManager,
    public IClrEventsReceiver
{
public:
    EtwEventsManager(
        IAllocationsListener* pAllocationListener,
        IContentionListener* pContentionListener,
        IGCSuspensionsListener* pGCSuspensionsListener
        );

// Inherited via IEtwEventsManager
    virtual void Register(IGarbageCollectionsListener* pGarbageCollectionsListener) override;
    virtual bool Start() override;
    virtual void Stop() override;

// Inherited via IClrEventsReceiver
    virtual void OnEvent(
        uint32_t tid,
        uint32_t version,
        uint64_t keyword,
        uint8_t level,
        uint32_t id,
        uint32_t cbEventData,
        const uint8_t* pEventData) override;
    virtual void OnStop() override;

private:
    std::unique_ptr<ClrEventsParser> _parser;
    std::unique_ptr<IpcClient> _ipcClient;
    std::unique_ptr<IpcServer> _ipcServer;
};
