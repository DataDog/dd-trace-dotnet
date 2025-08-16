// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "ClrLifetime.h"
#include "IClrLifetime.h"

ClrLifetime::ClrLifetime(std::atomic<bool>* pIsInitialized)
{
    _pIsInitialized = pIsInitialized;
}

bool ClrLifetime::IsInitialized() const
{
    auto IsInitialized = _pIsInitialized->load();
    return IsInitialized;
}
