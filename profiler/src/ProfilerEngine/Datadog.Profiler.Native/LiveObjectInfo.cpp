// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "LiveObjectInfo.h"

LiveObjectInfo::LiveObjectInfo(Sample& sample, uintptr_t address)
    : // TODO: we should be able to call _sample(sample) to copy a given Sample into another one
    _sample(sample.GetTimeStamp(), sample.GetRuntimeId(), sample.GetCallstack().size()),
    _address(address),
    _weakHandle(nullptr)
{
    _sample = sample.Copy();
}