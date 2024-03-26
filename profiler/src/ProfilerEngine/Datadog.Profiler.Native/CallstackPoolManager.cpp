// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "CallstackPoolManager.h"

CallstackPool* CallstackPoolManager::Get(std::size_t nbCallstacks)
{
    auto pool = std::make_unique<CallstackPool>(nbCallstacks);
    _pools.push_back(std::move(pool));
    return _pools.back().get();
}
