// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "TimerCreateCpuProfiler.h"

#include "CpuTimeProvider.h"
#include "IManagedThreadList.h"
#include "Log.h"
#include "OpSysTools.h"
#include "ProfilerSignalManager.h"

#include <libunwind.h>
#include <sys/syscall.h> /* Definition of SYS_* constants */
#include <sys/types.h>
#include <ucontext.h>
#include <unistd.h>

TimerCreateCpuProfiler* TimerCreateCpuProfiler::Instance = nullptr;

TimerCreateCpuProfiler::TimerCreateCpuProfiler(
    IConfiguration* pConfiguration,
    ProfilerSignalManager* pSignalManager,
    IManagedThreadList* pManagedThreadsList,
    CpuTimeProvider* pProvider,
    CallstackProvider callstackProvider) noexcept
    :
    _pSignalManager{pSignalManager}, // put it as parameter for better testing
    _pManagedThreadsList{pManagedThreadsList},
    _pProvider{pProvider},
    _callstackProvider{std::move(callstackProvider)},
    _samplingInterval{pConfiguration->GetCpuProfilingInterval()}
{
    Log::Info("Cpu profiling interval: ", _samplingInterval.count(), "ms");
    Log::Info("timer_create Cpu profiler is enabled");
}

TimerCreateCpuProfiler::~TimerCreateCpuProfiler()
{
    Stop();
}

void TimerCreateCpuProfiler::RegisterThread(std::shared_ptr<ManagedThreadInfo> threadInfo)
{
    std::shared_lock lock(_registerLock);

    if (GetState() != ServiceBase::State::Started)
    {
        return;
    }

    RegisterThreadImpl(threadInfo.get());
}

void TimerCreateCpuProfiler::UnregisterThread(std::shared_ptr<ManagedThreadInfo> threadInfo)
{
    std::shared_lock lock(_registerLock);

    auto timerId = threadInfo->SetTimerId(-1);

    if (timerId != -1)
    {
        Log::Debug("Unregister timer for thread ", threadInfo->GetOsThreadId());
        syscall(__NR_timer_delete, timerId);
    }
}

const char* TimerCreateCpuProfiler::GetName()
{
    return "timer_create-based Cpu Profiler";
}

bool TimerCreateCpuProfiler::StartImpl()
{
    if (_pSignalManager == nullptr)
    {
        Log::Info("Profiler Signal manager was not correctly initialized (see previous messages).",
                  "timer_create-based CPU profiler is disabled.");
        return false;
    }

    // If the signal is hijacked, what to do?
    auto registered = _pSignalManager->RegisterHandler(TimerCreateCpuProfiler::CollectStackSampleSignalHandler);

    if (registered)
    {
        std::unique_lock lock(_registerLock);
        Instance = this;

        // Create and start timer for all threads.
        _pManagedThreadsList->ForEach([this](ManagedThreadInfo* thread) { RegisterThreadImpl(thread); });
    }

    return registered;
}

bool TimerCreateCpuProfiler::StopImpl()
{
    _pSignalManager->UnRegisterHandler();
    Instance = nullptr;

    return true;
}

bool TimerCreateCpuProfiler::CollectStackSampleSignalHandler(int sig, siginfo_t* info, void* ucontext)
{
    auto instance = Instance;
    if (instance == nullptr)
    {
        return false;
    }

    return instance->Collect(ucontext);
}

bool TimerCreateCpuProfiler::Collect(void* ctx)
{
    auto threadInfo = ManagedThreadInfo::CurrentThreadInfo;
    if (threadInfo == nullptr)
    {
        // Ooops should never happen
        return false;
    }

    auto* context = reinterpret_cast<unw_context_t*>(ctx);

    auto callstack = _callstackProvider.Get();

    if (callstack.Capacity() <= 0)
    {
        return false;
    }

    auto buffer = callstack.Data();
    auto count = unw_backtrace2((void**)buffer.data(), buffer.size(), context, UNW_INIT_SIGNAL_FRAME);
    callstack.SetCount(count);

    if (count == 0)
    {
        // TODO a metric on event without callstack ?
        return false;
    }

    RawCpuSample rawCpuSample;

    std::tie(rawCpuSample.LocalRootSpanId, rawCpuSample.SpanId) = threadInfo->GetTracingContext();

    rawCpuSample.Timestamp = OpSysTools::GetTimestampSafe();
    rawCpuSample.AppDomainId = threadInfo->GetAppDomainId();
    rawCpuSample.Stack = std::move(callstack);
    rawCpuSample.ThreadInfo = std::move(threadInfo);
    rawCpuSample.Duration = _samplingInterval.count();
    _pProvider->Add(std::move(rawCpuSample));

    return true;
}

void TimerCreateCpuProfiler::RegisterThreadImpl(ManagedThreadInfo* threadInfo)
{
    auto timerId = threadInfo->GetTimerId();
    auto tid = threadInfo->GetOsThreadId();

    if (timerId != -1)
    {
        // already register (lost the race)
        Log::Debug("Timer was already created for thread ", tid);
        return;
    }

    Log::Debug("Creating timer for thread ", tid);

    struct sigevent sev;
    sev.sigev_value.sival_ptr = nullptr;
    sev.sigev_signo = _pSignalManager->GetSignal();
    sev.sigev_notify = SIGEV_THREAD_ID;
    ((int*)&sev.sigev_notify)[1] = tid;

    // Use raw syscalls, since libc wrapper allows only predefined clocks
    clockid_t clock = ((~tid) << 3) | 6; // CPUCLOCK_SCHED | CPUCLOCK_PERTHREAD_MASK thread_cpu_clock(tid);
    if (syscall(__NR_timer_create, clock, &sev, &timerId) < 0)
    {
        Log::Error("Call to timer_create failed for thread ", tid);
        return;
    }

    std::int32_t _interval = std::chrono::duration_cast<std::chrono::nanoseconds>(_samplingInterval).count();
    struct itimerspec ts;
    ts.it_interval.tv_sec = (time_t)(_interval / 1000000000);
    ts.it_interval.tv_nsec = _interval % 1000000000;
    ts.it_value = ts.it_interval;
    syscall(__NR_timer_settime, timerId, 0, &ts, nullptr);
}
