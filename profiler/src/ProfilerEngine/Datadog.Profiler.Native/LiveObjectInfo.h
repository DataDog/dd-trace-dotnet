// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <atomic>
#include <chrono>
#include <memory>

#include "cor.h"
#include "corprof.h"

#include "Sample.h"

class LiveObjectInfo
{
public:
    LiveObjectInfo(std::shared_ptr<Sample> sample, uintptr_t address, std::chrono::nanoseconds timestamp);

    // accessors
    void SetHandle(ObjectHandleID handle);
    ObjectHandleID GetHandle() const;
    uintptr_t GetAddress() const;
    std::shared_ptr<Sample> GetSample() const;
    void IncrementGC();
    bool IsGen2() const;

private:
    std::shared_ptr<Sample> _sample;
    uintptr_t _address;
    ObjectHandleID _weakHandle;
    // TODO This field is not yet used because the current implementation is incomplete.
    // Just keep to remind us to finish the implementation.
    std::chrono::nanoseconds _timestamp;
    uint64_t _gcCount;

    static std::atomic<uint64_t> s_nextObjectId;
};
