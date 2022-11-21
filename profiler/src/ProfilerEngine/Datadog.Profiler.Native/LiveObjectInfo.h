// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "cor.h"
#include "corprof.h"

#include "Sample.h"

class LiveObjectInfo
{
public:
    LiveObjectInfo(std::shared_ptr<Sample> sample, uintptr_t address);

    // accessors
    void SetHandle(ObjectHandleID handle);
    ObjectHandleID GetHandle() const;
    uintptr_t GetAddress() const;
    std::shared_ptr<Sample> GetSample() const;

private:
    std::shared_ptr<Sample> _sample;
    uintptr_t _address;
    ObjectHandleID _weakHandle;
};
