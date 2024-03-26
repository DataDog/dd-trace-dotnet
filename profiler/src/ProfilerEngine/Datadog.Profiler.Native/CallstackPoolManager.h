// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "CallstackPool.h"

#include <cstddef>
#include <vector>
#include <memory>

class CallstackPoolManager
{
public:
    CallstackPool* Get(std::size_t nbCallstacks);

private:

    std::vector<std::unique_ptr<CallstackPool>> _pools;
};
