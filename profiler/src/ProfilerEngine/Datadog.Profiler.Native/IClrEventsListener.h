// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "stdint.h"

class IClrEventsListener
{
public:
    virtual void OnEventReceived(
        uint32_t threadId,
        uint64_t keywords,
        uint32_t id,
        uint32_t version,
        uint32_t cbEventData,
        const uint8_t* eventData
        ) = 0;

    virtual ~IClrEventsListener() = default;
};