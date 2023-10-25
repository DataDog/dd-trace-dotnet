// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "IFrameStore.h"
#include "ISamplesProvider.h"
#include "IThreadInfo.h"
#include "NativeThreadsCpuProviderBase.h"
#include "MetricsRegistry.h"
#include "MeanMaxMetric.h"

class GCThreadsCpuProvider : public NativeThreadsCpuProviderBase
{
public:
    GCThreadsCpuProvider(CpuTimeProvider* cpuTimeProvider, MetricsRegistry& metricsRegistry);

    // Inherited via ISamplesProvider
    const char* GetName() override;

protected:
    void OnCpuDuration(std::uint64_t cpuTime) override;

private:
    bool IsGcThread(std::shared_ptr<IThreadInfo> const& thread);
    std::vector<std::shared_ptr<IThreadInfo>> const& GetThreads() override;
    std::vector<FrameInfoView> GetFrames() override;

    std::vector<std::shared_ptr<IThreadInfo>> _gcThreads;
    std::uint8_t _number_of_attempts = 0;
    std::shared_ptr<MeanMaxMetric> _cpuDurationMetric;
};
