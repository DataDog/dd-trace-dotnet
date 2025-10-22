// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once
#include <unordered_map>
#include "IFrameStore.h"

namespace libdatadog {
    class SymbolsStore;
}

class FrameStoreHelper : public IFrameStore
{
public:
    FrameStoreHelper(bool isManaged, std::string prefix, size_t count, libdatadog::SymbolsStore* symbolsStore);
    ~FrameStoreHelper();

public:
    // Inherited via IFrameStore
    std::pair<bool, MyFrameInfo> GetFrame(uintptr_t instructionPointer) override;
    bool GetTypeName(ClassID classId, std::string& name) override;
    bool GetTypeName(ClassID classId, std::string_view& name) override;

private:
    struct FrameInfo
    {
    public:
        ddog_prof_FunctionId FunctionId;
        ddog_prof_MappingId ModuleId;
        std::uint32_t StartLine;

        operator MyFrameInfo() const
        {
            return {FunctionId, ModuleId, StartLine};
        }
    };
    std::unordered_map<uintptr_t, std::pair<bool, FrameInfo>> _mapping;

    libdatadog::SymbolsStore* _symbolsStore;
    // Inherited via IFrameStore
};
