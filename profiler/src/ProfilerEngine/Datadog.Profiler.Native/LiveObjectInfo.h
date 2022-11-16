// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "Sample.h"

// wait for .NET 7 IcorProfilerInfo13
typedef void** ObjectHandleID;

class LiveObjectInfo
{
public:
    LiveObjectInfo(Sample&& sample, uintptr_t address);

    // move only class
    LiveObjectInfo& operator=(const LiveObjectInfo& info) = delete;
    LiveObjectInfo(const LiveObjectInfo&) = delete;
    LiveObjectInfo(LiveObjectInfo&& info) noexcept;
    LiveObjectInfo& operator=(LiveObjectInfo&& other) noexcept;

    // accessors
    void SetHandle(ObjectHandleID handle);
    ObjectHandleID GetHandle() const;
    uintptr_t GetAddress() const;
    const Sample& GetSample() const;

private:
    Sample _sample;
    uintptr_t _address;
    ObjectHandleID _weakHandle;

};
