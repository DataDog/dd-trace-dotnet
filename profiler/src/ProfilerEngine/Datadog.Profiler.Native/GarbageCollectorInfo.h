// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "IGarbageCollectorInfo.h"
#include "IThreadInfo.h"

class GarbageCollectorInfo : public IGarbageCollectorInfo
{
public:
    // Inherited via IGarbageCollectorInfo
    std::vector<std::shared_ptr<IThreadInfo>> const& GetThreads() override;

private:
    static bool IsGcThread(std::shared_ptr<IThreadInfo> const& thread);

    std::vector<std::shared_ptr<IThreadInfo>> _gcThreads;

    std::uint8_t _number_of_attempts = 0;
};