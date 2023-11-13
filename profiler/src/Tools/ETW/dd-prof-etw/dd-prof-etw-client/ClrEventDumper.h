// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
#pragma once

#include "..\..\..\..\ProfilerEngine\Datadog.Profiler.Native\IClrEventsReceiver.h"
#include "..\..\..\..\ProfilerEngine\Datadog.Profiler.Native.Windows\ETW\Protocol.h"

#include <string>

class ClrEventDumper : public IClrEventsReceiver
{
public:
    // Inherited via IClrEventsReceiver
    void OnEvent(
        uint32_t tid,
        uint32_t version,
        uint64_t keyword,
        uint8_t level,
        uint32_t id,
        uint32_t cbEventData,
        const uint8_t* pEventData) override;
    void OnStop() override;

private:
    bool BuildClrEvent(
        std::string& name,
        uint32_t tid, uint8_t version, uint16_t id, uint64_t keyword, uint8_t level);
};
