// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "IProcessSamplesProvider.h"
#include "IThreadInfo.h"

class CpuTimeProvider;
class IGarbageCollectorInfo;

class ProcessSamplesProvider : public IProcessSamplesProvider
{
public:
    ProcessSamplesProvider(IGarbageCollectorInfo* gcInfo, CpuTimeProvider* cpuTimeProvider);
    ~ProcessSamplesProvider() override = default;

    // Inherited via IProcessCollector
    std::list<std::shared_ptr<Sample>> GetSamples() override;

    const char* GetName() override;

private:
    std::uint64_t GetGcThreadsCpuTime();
    void CollectGcThreadsSamples(std::list<std::shared_ptr<Sample>>& samples);

    IGarbageCollectorInfo* _gcInfo;
    CpuTimeProvider* const _cpuTimeProvider;
};