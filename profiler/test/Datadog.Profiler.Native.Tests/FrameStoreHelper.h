// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once
#include <unordered_map>
#include "IFrameStore.h"

class FrameStoreHelper : public IFrameStore
{
public:
    FrameStoreHelper(bool isManaged, std::string prefix, size_t count);

public:
    // Inherited via IFrameStore
    std::tuple<bool, std::string, std::string> GetFrame(uintptr_t instructionPointer) override;

private:
    std::unordered_map<uintptr_t, std::tuple<bool, std::string, std::string>> _mapping;
};
