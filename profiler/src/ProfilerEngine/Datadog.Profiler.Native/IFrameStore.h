// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once
#include <string>
#include "cor.h"
#include "corprof.h"


class IFrameStore
{
public:
    virtual ~IFrameStore() = default;

    // return
    //  - true if managed frame
    //  - module name
    //  - frame text
    virtual std::tuple<bool, std::string, std::string> GetFrame(uintptr_t instructionPointer) = 0;
};
