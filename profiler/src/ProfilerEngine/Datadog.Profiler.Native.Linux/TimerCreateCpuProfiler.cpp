// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "TimerCreateCpuProfiler.h"

#include "CpuSampleProvider.h"
#include "DiscardMetrics.h"
#include "IManagedThreadList.h"
#include "Log.h"
#include "OpSysTools.h"
#include "ProfilerSignalManager.h"
#include "IConfiguration.h"

#include <libunwind.h>
#include <sys/syscall.h> /* Definition of SYS_* constants */
#include <sys/types.h>
#include <ucontext.h>
#include <unistd.h>

std::atomic<TimerCreateCpuProfiler*> TimerCreateCpuProfiler::Instance = nullptr;

TimerCreateCpuProfiler::TimerCreateCpuProfiler(
    IConfiguration* pConfiguration,
    ProfilerSignalManager* pSignalManager,
    IManagedThreadList* pManagedThreadsList,
    CpuSampleProvider* pProvider,
    MetricsRegistry& metricsRegistry) noexcept
    :
    _pSignalManager{pSignalManager}, // put it as parameter for better testing
    _pManagedThreadsList{pManagedThreadsList},
    _pProvider{pProvider},
    _samplingInterval{pConfiguration->GetCpuProfilingInterval()},
    _nbThreadsInSignalHandler{0}
{
    Log::Info("Cpu profiling interval: ", _samplingInterval.count(), "ms");
    Log::Info("timer_create Cpu profiler is enabled");
    _totalSampling = metricsRegistry.GetOrRegister<CounterMetric>("dotnet_cpu_sampling_requests");
    _discardMetrics = metricsRegistry.GetOrRegister<DiscardMetrics>("dotnet_cpu_sample_discarded");
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
    UnregisterThreadImpl(threadInfo.get());
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
        _pManagedThreadsList->ForEach([this](ManagedThreadInfo* thread) {
            // only register threads that have an OS thread id
            if (thread->GetOsThreadId() != 0)
            {
                RegisterThreadImpl(thread);
            }
        });
    }

    return registered;
}

bool TimerCreateCpuProfiler::StopImpl()
{
    Instance = nullptr;
    // we cannot unregister. We would replace the current action by the default one
    // which will cause the termination of the process
    // Instead, mark SIGPROF as ignored.
    if (!_pSignalManager->IgnoreSignal())
    {
        Log::Warn("Failed to mark the signal SIGPROF as ignored.");
    }

    {
        std::unique_lock lock(_registerLock);

        // We have to remove all timers before unregistering the handler for SIGPROF.
        // Otherwise, the process will end with exit code 155 (128 + 27 => 27 being SIGPROF value)
        _pManagedThreadsList->ForEach([this](ManagedThreadInfo* thread) { UnregisterThreadImpl(thread); });
    }

    std::uint64_t nbThreadsInSignalHandler = _nbThreadsInSignalHandler;
    if (nbThreadsInSignalHandler != 0)
    {
        Log::Info("Waiting for all threads exiting the signal handler (#threads ", _nbThreadsInSignalHandler, ")");
    
        // TODO: for now we sleep.
        std::this_thread::sleep_for(500ms);
        if (_nbThreadsInSignalHandler != 0)
        {
            Log::Warn("There are threads that are still executing the signal handler: ", _nbThreadsInSignalHandler);
            return false;
        }
        Log::Info("All threads exited the signal handler");
    }

    return true;
}

bool TimerCreateCpuProfiler::CollectStackSampleSignalHandler(int sig, siginfo_t* info, void* ucontext)
{
    auto instance = Instance.load();
    if (instance == nullptr)
    {
        return false;
    }

    return instance->Collect(ucontext);
}

// This symbol is defined in the Datadog.Linux.ApiWrapper. It allows us to check if the thread to be profiled
// contains a frame of a function that might cause a deadlock.
extern "C" unsigned long long dd_inside_wrapped_functions() __attribute__((weak));

bool TimerCreateCpuProfiler::CanCollect(void* ctx)
{
    if (dd_inside_wrapped_functions != nullptr && dd_inside_wrapped_functions() != 0)
    {
        _discardMetrics->Incr<DiscardReason::InsideWrappedFunction>();
        return false;
    }

    auto* context = reinterpret_cast<ucontext_t*>(ctx);
    // If SIGSEGV is part of the sigmask set, it means that the thread was executing
    // the SIGSEGV signal handler (or someone blocks SIGSEGV signal for this thread,
    // but that less likely)
    if (sigismember(&(context->uc_sigmask), SIGSEGV) == 1)
    {
        _discardMetrics->Incr<DiscardReason::InSegvHandler>();
        return false;
    }

    return true;
}

struct ErrnoSaveAndRestore
{
public:
    ErrnoSaveAndRestore() :
        _oldErrno{errno}
    {
    }
    ~ErrnoSaveAndRestore()
    {
        errno = _oldErrno;
    }

private:
    int _oldErrno;
};

struct StackWalkLock
{
public:
    StackWalkLock(std::shared_ptr<ManagedThreadInfo> threadInfo) :
        _threadInfo{std::move(threadInfo)}
    {
        // Do not call lock while being in the signal handler otherwise
        // we might end in a deadlock situation (lock inversion...)
        _lockTaken = _threadInfo->TryAcquireLock();
    }

    ~StackWalkLock()
    {
        if (_lockTaken)
        {
            _threadInfo->ReleaseLock();
        }
    }

    bool IsLockAcquired() const
    {
        return _lockTaken;
    }

private:
    std::shared_ptr<ManagedThreadInfo> _threadInfo;
    bool _lockTaken;
};

bool TimerCreateCpuProfiler::Collect(void* ctx)
{
    _nbThreadsInSignalHandler++;
    _totalSampling->Incr();

    auto threadInfo = ManagedThreadInfo::CurrentThreadInfo;
    if (threadInfo == nullptr)
    {
        _discardMetrics->Incr<DiscardReason::UnknownThread>();
        _nbThreadsInSignalHandler--;
        // Ooops should never happen
        return false;
    }

    StackWalkLock l(threadInfo);
    if (!l.IsLockAcquired())
    {
        _discardMetrics->Incr<DiscardReason::FailedAcquiringLock>();
        _nbThreadsInSignalHandler--;
        return false;
    }

    if (!CanCollect(ctx))
    {
        _nbThreadsInSignalHandler--;
        return false;
    }

    // Libunwind can overwrite the value of errno - save it beforehand and restore it at the end
    ErrnoSaveAndRestore errnoScope;

    auto rawCpuSample = _pProvider->GetRawSample();
    if (!rawCpuSample)
    {
        _nbThreadsInSignalHandler--;
        return false;
    }

    auto buffer = rawCpuSample->Stack.AsSpan();
    auto* context = reinterpret_cast<unw_context_t*>(ctx);
    auto count = unw_backtrace2((void**)buffer.data(), buffer.size(), context, UNW_INIT_SIGNAL_FRAME);
    rawCpuSample->Stack.SetCount(count);

    if (count == 0)
    {
        rawCpuSample.Discard();
        _discardMetrics->Incr<DiscardReason::EmptyBacktrace>();
        _nbThreadsInSignalHandler--;
        return false;
    }

    // TO FIX this breaks the CI Visibility.
    // No Cpu samples will have the predefined span id, root local span id
    std::tie(rawCpuSample->LocalRootSpanId, rawCpuSample->SpanId) = threadInfo->GetTracingContext();

    rawCpuSample->Timestamp = OpSysTools::GetTimestampSafe();
    rawCpuSample->AppDomainId = threadInfo->GetAppDomainId();
    rawCpuSample->ThreadInfo = std::move(threadInfo);
    rawCpuSample->Duration = _samplingInterval;
    _nbThreadsInSignalHandler--;
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

    threadInfo->SetTimerId(timerId);

    std::int32_t _interval = std::chrono::duration_cast<std::chrono::nanoseconds>(_samplingInterval).count();
    struct itimerspec ts;
    ts.it_interval.tv_sec = (time_t)(_interval / 1000000000);
    ts.it_interval.tv_nsec = _interval % 1000000000;
    ts.it_value = ts.it_interval;
    syscall(__NR_timer_settime, timerId, 0, &ts, nullptr);
}

void TimerCreateCpuProfiler::UnregisterThreadImpl(ManagedThreadInfo* threadInfo)
{
    auto timerId = threadInfo->SetTimerId(-1);

    if (timerId != -1)
    {
        Log::Debug("Unregister timer for thread ", threadInfo->GetOsThreadId());
        syscall(__NR_timer_delete, timerId);
    }
}