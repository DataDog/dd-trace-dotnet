// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "ClrLifetime.h"
#include "IClrLifetime.h"

ClrLifetime::ClrLifetime(std::atomic<bool>* pIsRunning)
{
    _pIsRunning = pIsRunning;
}

bool ClrLifetime::IsRunning() const
{
    auto isRunning = _pIsRunning->load();
    return isRunning;
}
