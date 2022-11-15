// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "LiveObjectInfo.h"

LiveObjectInfo::LiveObjectInfo(Sample&& sample, uintptr_t address)
    : // TODO: we should be able to call _sample(sample) to copy a given Sample into another one
    _sample(std::move(sample)),
    _address(address),
    _weakHandle(nullptr)
{
}

LiveObjectInfo::LiveObjectInfo(LiveObjectInfo&& info) noexcept
    :
    _sample(std::move(info._sample))
{
    _address = std::move(info._address);
    _weakHandle = std::move(info._weakHandle);
}

LiveObjectInfo& LiveObjectInfo::operator=(LiveObjectInfo&& other) noexcept
{
    _address = std::move(other._address);
    _weakHandle = std::move(other._weakHandle);
    _sample = std::move(other._sample);

    return *this;
}

void LiveObjectInfo::SetHandle(void** handle)
{
    _weakHandle = handle;
}

void** LiveObjectInfo::GetHandle()
{
    return _weakHandle;
}


uintptr_t LiveObjectInfo::GetAddress()
{
    return _address;
}
