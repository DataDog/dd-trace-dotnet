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
    std::pair<bool, FrameInfoView> GetFrame(uintptr_t instructionPointer) override;
    bool GetTypeName(ClassID classId, std::string& name) override;
    bool GetTypeName(ClassID classId, std::string_view& name) override;

private:
    struct FrameInfo
    {
    public:
        std::string ModuleName;
        std::string Frame;
        std::string_view Filename;
        std::uint32_t StartLine;

        operator FrameInfoView() const
        {
            return {ModuleName, Frame, Filename, StartLine};
        }
    };
    std::unordered_map<uintptr_t, std::pair<bool, FrameInfo>> _mapping;

    // Inherited via IFrameStore
};
