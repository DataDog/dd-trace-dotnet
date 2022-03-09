// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include <sstream>
#include "FrameStoreHelper.h"

FrameStoreHelper::FrameStoreHelper(bool isManaged, std::string prefix, size_t count)
{
    // build automatically a mapping
    //    number --> { isManaged, "module #number", "prefix #number" }
    // with number going from 1 to count
    for (size_t i = 1; i <= count; i++)
    {
        std::stringstream frameBuilder;
        frameBuilder << prefix << " #" << i;

        std::stringstream moduleBuilder;
        moduleBuilder << "module #" << i;

        _mapping[i] = { isManaged, moduleBuilder.str(), frameBuilder.str() };
    }
}


//FrameStoreHelper::FrameStoreHelper(std::unordered_map<uintptr_t, std::string> mapping)
//    :
//    _mapping{mapping}
//{
//}


std::tuple<bool, std::string, std::string> FrameStoreHelper::GetFrame(uintptr_t instructionPointer)
{
    auto item = _mapping.find(instructionPointer);
    if (item != _mapping.end())
    {
        return item->second;
    }

    return { true, "module???", "frame???" };
}
