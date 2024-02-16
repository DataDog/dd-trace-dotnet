// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <cstdint>


class IEtwEventsReceiver
{
public:
    virtual void OnEvent(
        uint64_t timestamp,  // the events timestamp is in System Time for GMT
        uint32_t tid,
        uint32_t version,
        uint64_t keyword,
        uint8_t level,
        uint32_t id,
        uint32_t cbEventData,
        const uint8_t* pEventData) = 0;
    virtual void OnStop() = 0;

    virtual ~IEtwEventsReceiver() = default;
};