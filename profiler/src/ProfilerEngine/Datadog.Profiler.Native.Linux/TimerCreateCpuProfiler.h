// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "CounterMetric.h"
#include "ManagedThreadInfo.h"
#include "MetricsRegistry.h"
#include "ServiceBase.h"

#include <signal.h>

#include <atomic>
#include <chrono>
#include <memory>
#include <shared_mutex>
#include <unordered_set>

class DiscardMetrics;
class IConfiguration;
class IThreadInfo;
class IManagedThreadList;
class ProfilerSignalManager;
class CpuSampleProvider;
class IUnwinder;

class TimerCreateCpuProfiler : public ServiceBase
{
public:
    TimerCreateCpuProfiler(
        IConfiguration* pConfiguration,
        ProfilerSignalManager* pSignalManager,
        IManagedThreadList* pManagedThreadsList,
        CpuSampleProvider* pProvider,
        MetricsRegistry& metricsRegistry,
        IUnwinder* pUnwinder) noexcept;

    ~TimerCreateCpuProfiler();

    void RegisterThread(std::shared_ptr<ManagedThreadInfo> threadInfo);
    void UnregisterThread(std::shared_ptr<ManagedThreadInfo> threadInfo);

    const char* GetName() override;

private:
    static bool CollectStackSampleSignalHandler(int sig, siginfo_t* info, void* ucontext);
    static std::atomic<TimerCreateCpuProfiler*> Instance;

    bool CanCollect(void* context);
    bool Collect(void* ucontext);
    void RegisterThreadImpl(ManagedThreadInfo* thread);
    void UnregisterThreadImpl(ManagedThreadInfo* threadInfo);

    bool StartImpl() override;
    bool StopImpl() override;

    ProfilerSignalManager* _pSignalManager;
    IManagedThreadList* _pManagedThreadsList;
    CpuSampleProvider* _pProvider;
    std::chrono::milliseconds _samplingInterval;
    std::shared_mutex _registerLock;
    std::shared_ptr<CounterMetric> _totalSampling;
    std::shared_ptr<DiscardMetrics> _discardMetrics;
    std::atomic<std::uint64_t> _nbThreadsInSignalHandler;
    std::unique_ptr<IUnwinder> _pUnwinder;
};