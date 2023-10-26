// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once
#include "cor.h"
#include "corprof.h"

#include <string>
#include <cstdint>

struct FrameInfoView
{
public:
    std::string_view ModuleName;
    std::string_view Frame;
    std::string_view Filename;
    std::uint32_t StartLine;
};

class IFrameStore
{
public:
    virtual ~IFrameStore() = default;

    // return
    //  - true if managed frame
    virtual std::pair<bool, FrameInfoView> GetFrame(uintptr_t instructionPointer) = 0;
    virtual bool GetTypeName(ClassID classId, std::string& name) = 0;
    virtual bool GetTypeName(ClassID classId, std::string_view& name) = 0;
};
