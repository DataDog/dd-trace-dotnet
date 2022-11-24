// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "LiveObjectInfo.h"

LiveObjectInfo::LiveObjectInfo(std::shared_ptr<Sample> sample, uintptr_t address, int64_t timestamp)
    :
    _address(address),
    _weakHandle(nullptr),
    _timestamp(timestamp),
    _gcCount(0)
{
    sample->AddLabel(Label{Sample::ObjectLifetimeLabel, std::to_string(0)});
    _sample = sample;
}

void LiveObjectInfo::SetHandle(ObjectHandleID handle)
{
    _weakHandle = handle;
}

ObjectHandleID LiveObjectInfo::GetHandle() const
{
    return _weakHandle;
}

uintptr_t LiveObjectInfo::GetAddress() const
{
    return _address;
}

std::shared_ptr<Sample> LiveObjectInfo::GetSample() const
{
    return _sample;
}

void LiveObjectInfo::IncrementGC()
{
    _gcCount++;
}

bool LiveObjectInfo::IsGen2() const
{
    return _gcCount >= 2;
}
