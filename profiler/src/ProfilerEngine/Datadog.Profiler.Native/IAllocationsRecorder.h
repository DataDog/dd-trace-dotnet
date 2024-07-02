// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

// from dotnet coreclr includes
#include "cor.h"
#include "corprof.h"
// end

#include <string>

class IAllocationsRecorder
{
public:
    virtual ~IAllocationsRecorder() = default;
    virtual void OnObjectAllocated(ObjectID objectID, ClassID classID) = 0;
    virtual bool Serialize(const std::string& filename) = 0;
};