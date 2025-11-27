// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

// from dotnet coreclr includes
#include "cor.h"
#include "corprof.h"
// end

#include "CounterMetric.h"
#include "MetricsRegistry.h"
#include "StackFramesCollectorBase.h"

#include <atomic>
#include <condition_variable>
#include <memory>
#include <mutex>
#include <signal.h>
#include <unordered_map>
#include <array>
#include <cstdint>
#include <sys/types.h>

class IManagedThreadList;
class ProfilerSignalManager;
class ProfilerSignalManager;
class IConfiguration;
class CallstackProvider;
class DiscardMetrics;

// libunwind includes for hybrid unwinding
#include <libunwind.h>

class LinuxStackFramesCollector : public StackFramesCollectorBase
{
public:
    explicit LinuxStackFramesCollector(
        ProfilerSignalManager* signalManager,
        IConfiguration const* configuration,
        CallstackProvider* callstackProvider,
        MetricsRegistry& metricsRegistry);
    ~LinuxStackFramesCollector() override;

    LinuxStackFramesCollector(LinuxStackFramesCollector const&) = delete;
    LinuxStackFramesCollector& operator=(LinuxStackFramesCollector const&) = delete;

    // Public static method for CPU profiler to use hybrid unwinding
    static std::int32_t CollectStackHybridStatic(void* ctx, uintptr_t* buffer, size_t bufferSize);

protected:
    // Linux collector is different from Windows:
    // There is no notion to Suspend/Resume a thread and to have an external thread walk the suspended thread.
    // The thread that will call CollectStackSample(..), will send a signal to the target thread and then
    // wait until the target thread finished walking its callstack.
    // So, for ResumeThread and SuspendThread are No Ops for this collector, and we defer to the respective baseclass No-Op methods.

    StackSnapshotResultBuffer* CollectStackSampleImplementation(ManagedThreadInfo* pThreadInfo,
                                                                uint32_t* pHR,
                                                                bool selfCollect) override;

private:
    class ErrorStatistics
    {
    public:
        void Add(std::int32_t errorCode);
        void Log();

    private:
        //                 v- error code v- # of errors
        std::unordered_map<std::int32_t, std::int32_t> _stats;
    };

private:
    void NotifyStackWalkCompleted(std::int32_t resultErrorCode);
    void UpdateErrorStats(std::int32_t errorCode);
    static bool ShouldLogStats();
    bool CanCollect(int32_t threadId, siginfo_t* info, void* ucontext) const;
    std::int32_t CollectStackManually(void* ctx);
    std::int32_t CollectStackWithBacktrace2(void* ctx);
    std::int32_t CollectStackHybrid(void* ctx);
    void MarkAsInterrupted();

    // Hybrid unwinding helper methods
    bool IsManagedCode(uintptr_t instructionPointer);
    std::int32_t UnwindManagedFrameManually(unw_cursor_t* cursor, uintptr_t ip, unw_context_t* original_context);
    std::int32_t WalkManagedStackChain(uintptr_t initial_ip, uintptr_t initial_fp, uintptr_t initial_sp);
    bool ReadStackMemory(uintptr_t address, void* buffer, size_t size);
    bool IsValidReturnAddress(uintptr_t address);
    size_t EstimateStackFrameSize(uintptr_t ip);

    std::int32_t _lastStackWalkErrorCode;
    std::condition_variable _stackWalkInProgressWaiter;
    // since we wait for a specific amount of time, if a call to notify_one
    // is done while we are not waiting, we will miss it and
    // we will block for ever.
    // This flag is used to prevent blocking on successfull (but long) stackwalking
    std::atomic<bool> _stackWalkFinished;
    pid_t _processId;
    ProfilerSignalManager* _signalManager;

private:
    static bool CollectStackSampleSignalHandler(int sig, siginfo_t* info, void* ucontext);

    static std::mutex s_stackWalkInProgressMutex;

    static LinuxStackFramesCollector* s_pInstanceCurrentlyStackWalking;

    std::int32_t CollectCallStackCurrentThread(void* ucontext);

    ErrorStatistics _errorStatistics;
    bool _useBacktrace2;
    bool _useHybridUnwinding;
    std::shared_ptr<CounterMetric> _samplingRequest;

    std::shared_ptr<DiscardMetrics> _discardMetrics;

    enum class HybridTraceEvent : uint8_t
    {
        Start,
        AbortRequested,
        GetContextFailed,
        InitFailed,
        GetIpFailed,
        AddFrameFailed,
        ManagedFrame,
        NativeFrame,
        ManualStart,
        ManualFramePointerUnavailable,
        ManualFramePointerReadFailed,
        ManualFramePointerInvalidReturn,
        ManualFramePointerSuccess,
        ManualLinkRegisterSuccess,
        ManualFallback,
        StepResult,
        Finish,
        ManagedViaJitCache,
        ManagedViaProcMaps,
        ManagedDetectionMiss,
        CacheMissing,
    };

    struct HybridTraceEntry
    {
        HybridTraceEvent Event;
        uintptr_t Value;
        uintptr_t Aux;
        std::int32_t Result;
    };

    struct HybridTraceBuffer
    {
        static constexpr std::size_t MaxEntries = 128;

        void Reset(pid_t threadId, uintptr_t contextPointer);
        void SetInitFlags(std::uint32_t flags);
        void Append(HybridTraceEvent event, uintptr_t value, uintptr_t aux, std::int32_t result);
        std::uint32_t Count() const;
        bool HasOverflow() const;
        HybridTraceEntry EntryAt(std::size_t index) const;
        pid_t GetThreadId() const { return _threadId; }
        uintptr_t GetContextPointer() const { return _contextPointer; }
        std::uint32_t GetInitFlags() const { return _initFlags; }
        void ResetAfterFlush();

    private:
        pid_t _threadId{0};
        uintptr_t _contextPointer{0};
        std::uint32_t _initFlags{0};
        std::atomic<std::uint32_t> _count{0};
        std::atomic<bool> _overflow{false};
        std::array<HybridTraceEntry, MaxEntries> _entries{};
    };

    void RecordHybridEvent(HybridTraceEvent event, uintptr_t value = 0, uintptr_t aux = 0, std::int32_t result = 0);
    void FlushHybridTrace(std::int32_t finalResult);
    static const char* HybridTraceEventName(HybridTraceEvent event);

    HybridTraceBuffer _hybridTrace;
};