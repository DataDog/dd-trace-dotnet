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
    _processId{OpSysTools::GetProcId()},
    _serviceState{ServiceState::Initialized},
    _samplingInterval{pConfiguration->GetCpuProfilingInterval()}
{
    Log::Info("Cpu profiling interval: ", _samplingInterval.count(), "ms");

    Instance = this;
}

TimerCreateCpuProfiler::~TimerCreateCpuProfiler()
{
    Stop();
}

void TimerCreateCpuProfiler::RegisterThread(std::shared_ptr<ManagedThreadInfo> threadInfo)
{
    if (_serviceState != ServiceState::Started)
    {
        return;
    }

    auto tid = threadInfo->GetOsThreadId();
    Log::Debug("Creating timer for thread ", tid);

    struct sigevent sev;
    sev.sigev_value.sival_ptr = nullptr;
    sev.sigev_signo = _pSignalManager->GetSignal();
    sev.sigev_notify = SIGEV_THREAD_ID;
    ((int*)&sev.sigev_notify)[1] = tid;

    // Use raw syscalls, since libc wrapper allows only predefined clocks
    clockid_t clock = ((~tid) << 3) | 6; // CPUCLOCK_SCHED | CPUCLOCK_PERTHREAD_MASK thread_cpu_clock(tid);
    int timerId;
    if (syscall(__NR_timer_create, clock, &sev, &timerId) < 0)
    {
        Log::Error("Call to timer_create failed for thread ", tid);
        return;
    }

    auto oldTimerId = threadInfo->SetTimerId(timerId);

    // In case of SSI:
    // There is a race when Start() and RegisterThread :
    // The thread can be added while lookping over the in the managed threads list
    // and a call being made to RegisterThread
    // In that case, just keep the first timer id and delete the other
    if (oldTimerId != -1)
    {
        // delete the newly created timer
        syscall(__NR_timer_delete, timerId);
        threadInfo->SetTimerId(oldTimerId);
        return;
    }

    std::int32_t _interval = std::chrono::duration_cast<std::chrono::nanoseconds>(_samplingInterval).count();
    struct itimerspec ts;
    ts.it_interval.tv_sec = (time_t)(_interval / 1000000000);
    ts.it_interval.tv_nsec = _interval % 1000000000;
    ts.it_value = ts.it_interval;
    syscall(__NR_timer_settime, timerId, 0, &ts, nullptr);
}

void TimerCreateCpuProfiler::UnregisterThread(std::shared_ptr<ManagedThreadInfo> threadInfo)
{
    if (_serviceState != ServiceState::Started)
    {
        return;
    }

    auto timerId = threadInfo->SetTimerId(-1);

    Log::Debug("Unregister timer for thread ", threadInfo->GetOsThreadId());
    syscall(__NR_timer_delete, timerId);
}

const char* TimerCreateCpuProfiler::GetName()
{
    return "timer_create-based Cpu Profiler";
}

bool TimerCreateCpuProfiler::Start()
{
    if (_serviceState.exchange(ServiceState::Started) == ServiceState::Started)
    {
        // Log to say that it's already stated
        return true;
    }

    // If the signal is higjacked, what to do?
    auto registered = _pSignalManager->RegisterHandler(TimerCreateCpuProfiler::CollectStackSampleSignalHandler);

    auto it = _pManagedThreadsList->CreateIterator();

    ManagedThreadInfo* first = nullptr;

    auto current = _pManagedThreadsList->LoopNext(it);
    while (current != nullptr && current.get() != first)
    {
        if (first == nullptr)
        {
            first = current.get();
        }

        if (current->GetTimerId() != -1)
        {
            continue;
        }

        RegisterThread(current);
        current = _pManagedThreadsList->LoopNext(it);
    }

    return registered;
}

bool TimerCreateCpuProfiler::Stop()
{
    auto old = _serviceState.exchange(ServiceState::Stopped);
    if (old == ServiceState::Initialized)
    {
        // TODO Log must be started first
        return false;
    }

    if (old == ServiceState::Stopped)
    {
        // Maybe a race. Log to say that it's already stopped
        return true;
    }

    // TODO
    //_signalManager->UnRegisterHandler(SIGPROF);

    // for now it's ok not necessary to go through threads and delete the timer
    // If Stop is called, the process is going down.
    return true;
}

bool TimerCreateCpuProfiler::CollectStackSampleSignalHandler(int sig, siginfo_t* info, void* ucontext)
{
    return Instance->Collect(info->si_pid, ucontext);
}

bool TimerCreateCpuProfiler::Collect(pid_t callerProcess, void* ctx)
{
    if (_serviceState == ServiceState::Stopped)
    {
        return false;
    }

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
