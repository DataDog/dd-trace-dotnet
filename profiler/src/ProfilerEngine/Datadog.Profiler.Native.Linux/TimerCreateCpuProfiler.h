// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "IService.h"
#include "ManagedThreadInfo.h"
#include "CallstackProvider.h"

#include <signal.h>

#include <atomic>
#include <chrono>
#include <memory>
#include <unordered_set>

class IConfiguration;
class IThreadInfo;
class IManagedThreadList;
class ProfilerSignalManager;
class CpuTimeProvider;
class CallstackProvider;

class TimerCreateCpuProfiler : public IService
{
public:
    TimerCreateCpuProfiler(
        IConfiguration* pConfiguration,
        ProfilerSignalManager* pSignalManager,
        IManagedThreadList* pManagedThreadsList,
        CpuTimeProvider* pProvider,
        CallstackProvider calstackProvider) noexcept;

    ~TimerCreateCpuProfiler();

    void RegisterThread(std::shared_ptr<ManagedThreadInfo> threadInfo);
    void UnregisterThread(std::shared_ptr<ManagedThreadInfo> threadInfo);

    const char* GetName() override;
    bool Start() override;
    bool Stop() override;

private:
    static bool CollectStackSampleSignalHandler(int sig, siginfo_t* info, void* ucontext);
    static TimerCreateCpuProfiler* Instance;

    bool Collect(pid_t callerProcess, void* ucontext);

    enum class ServiceState
    {
        Started,
        Stopped,
        Initialized
    };

    ProfilerSignalManager* _pSignalManager;
    IManagedThreadList* _pManagedThreadsList;
    CpuTimeProvider* _pProvider;
    CallstackProvider _callstackProvider;
    pid_t _processId;
    std::atomic<ServiceState> _serviceState;
    std::chrono::milliseconds _samplingInterval;
};