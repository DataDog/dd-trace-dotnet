// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "Sample.h"

class LiveObjectInfo
{
public:
    LiveObjectInfo(Sample& sample, uintptr_t address);

private:
    Sample _sample;
    uintptr_t _address;
    void** _weakHandle;

};
