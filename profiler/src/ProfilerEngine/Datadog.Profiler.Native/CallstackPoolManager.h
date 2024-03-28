// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "CallstackPool.h"

#include "shared/src/native-src/dd_memory_resource.hpp"

#include <vector>

class CallstackPoolManager
{
public:
    static CallstackPool* GetDefault();

    CallstackPool* Get(shared::pmr::memory_resource* allocator);

private:

    std::vector<std::unique_ptr<CallstackPool>> _pools;
};