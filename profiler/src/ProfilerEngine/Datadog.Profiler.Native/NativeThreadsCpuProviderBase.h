// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <chrono>

#include "IFrameStore.h"
#include "ISamplesProvider.h"
#include "IThreadInfo.h"
#include "Sample.h"

#include <vector>

class CpuTimeProvider;

class NativeThreadsCpuProviderBase : public ISamplesProvider
{
public:
    NativeThreadsCpuProviderBase(CpuTimeProvider* cpuTimeProvider);

protected:
    virtual void OnCpuDuration(std::chrono::milliseconds cpuTime);

private:

    std::unique_ptr<SamplesEnumerator> GetSamples() override;
    virtual std::vector<FrameInfoView> GetFrames() = 0;
    virtual std::vector<std::shared_ptr<IThreadInfo>> const& GetThreads() = 0;
    virtual Labels GetLabels() = 0;

    CpuTimeProvider* _cpuTimeProvider;
    std::chrono::milliseconds _previousTotalCpuTime;
};
