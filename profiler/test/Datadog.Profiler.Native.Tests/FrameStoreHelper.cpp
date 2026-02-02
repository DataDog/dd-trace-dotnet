// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include <sstream>
#include "FrameStoreHelper.h"
#include "SymbolsStore.h"

FrameStoreHelper::FrameStoreHelper(bool isManaged, std::string prefix, size_t count, libdatadog::SymbolsStore* pSymbolsStore)
{
    _pSymbolsStore = pSymbolsStore;
    // build automatically a mapping
    //    number --> { isManaged, "module #number", "prefix #number" }
    // with number going from 1 to count
    for (size_t i = 1; i <= count; i++)
    {
        std::stringstream frameBuilder;
        frameBuilder << prefix << " #" << i;

        std::stringstream moduleBuilder;
        moduleBuilder << "module #" << i;

        auto functionId = _pSymbolsStore->InternFunction(frameBuilder.str(), "");
        auto moduleId = _pSymbolsStore->InternMapping(moduleBuilder.str());
        _mapping[i] = {isManaged, {moduleId.value(), functionId.value(), 0}};
    }
}

std::pair<bool, FrameInfoView> FrameStoreHelper::GetFrame(uintptr_t instructionPointer)
{
    auto item = _mapping.find(instructionPointer);
    if (item != _mapping.end())
    {
        return item->second;
    }

    return {true, FrameInfo{_pSymbolsStore->GetUnknownManagedAssemblyId(), _pSymbolsStore->GetUnknownManagedFrameId(), 0}};
}


bool FrameStoreHelper::GetTypeName(ClassID classId, std::string& name)
{
    return false;
}

bool FrameStoreHelper::GetTypeName(ClassID classId, std::string_view& name)
{
    return false;
}
