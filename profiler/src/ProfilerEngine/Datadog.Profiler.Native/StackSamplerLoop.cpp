// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "StackSamplerLoop.h"

#include <chrono>
#include <inttypes.h>
#include <iomanip>
#include <iostream>
#include <map>
#include <memory>
#include <sstream>
#include <stdio.h>

#include "Configuration.h"
#include "HResultConverter.h"
#include "ICollector.h"
#include "Log.h"
#include "ManagedThreadInfo.h"
#include "ManagedThreadList.h"
#include "OsSpecificApi.h"
#include "OpSysTools.h"
#include "RawCpuSample.h"
#include "RawWallTimeSample.h"
#include "ScopeFinalizer.h"
#include "StackFramesCollectorBase.h"
#include "StackSamplerLoopManager.h"
#include "ThreadsCpuManager.h"

#include "shared/src/native-src/string.h"

// Configuration constants:
using namespace std::chrono_literals;
constexpr std::chrono::nanoseconds SamplingPeriod = 9ms;
constexpr uint64_t SamplingPeriodMs = SamplingPeriod.count() / 1000000;
constexpr int32_t MaxThreadsPerIterationForWallTime = 5;
constexpr int32_t MaxThreadsPerIterationForCpuTime = 60;
constexpr const WCHAR* ThreadName = WStr("DD.Profiler.StackSamplerLoop.Thread");

#ifdef NDEBUG
// Release build logs collection stats every 30 mins:
constexpr uint64_t StackSamplerLoop_StackSnapshotResultsStats_LogPeriodNS = (30 * 60 * 1000000000ull);
#else
// Debug build build logs collection stats every 1 mins:
constexpr uint64_t StackSamplerLoop_StackSnapshotResultsStats_LogPeriodNS = (1 * 60 * 1000000000ull);
#endif

StackSamplerLoop::StackSamplerLoop(
    ICorProfilerInfo4* pCorProfilerInfo,
    IConfiguration* pConfiguration,
    StackFramesCollectorBase* pStackFramesCollector,
    StackSamplerLoopManager* pManager,
    IThreadsCpuManager* pThreadsCpuManager,
    IManagedThreadList* pManagedThreadList,
    ICollector<RawWallTimeSample>* pWallTimeCollector,
    ICollector<RawCpuSample>* pCpuTimeCollector)
    :
    _pCorProfilerInfo{pCorProfilerInfo},
    _pConfiguration{pConfiguration},
    _pStackFramesCollector{pStackFramesCollector},
    _pManager{pManager},
    _pThreadsCpuManager{pThreadsCpuManager},
    _pManagedThreadList{pManagedThreadList},
    _pWallTimeCollector{pWallTimeCollector},
    _pCpuTimeCollector{pCpuTimeCollector},
    _pLoopThread{nullptr},
    _loopThreadOsId{0},
    _targetThread(nullptr),
    _iteratorWallTime{0},
    _iteratorCpuTime{0}
{
    _pCorProfilerInfo->AddRef();

    _iteratorWallTime = _pManagedThreadList->CreateIterator();
    _iteratorCpuTime = _pManagedThreadList->CreateIterator();

    _pLoopThread = new std::thread(&StackSamplerLoop::MainLoop, this);
    OpSysTools::SetNativeThreadName(_pLoopThread, ThreadName);
}

StackSamplerLoop::~StackSamplerLoop()
{
    this->RequestShutdown();
    this->Join();

    ICorProfilerInfo4* corProfilerInfo = _pCorProfilerInfo;
    if (corProfilerInfo != nullptr)
    {
        _pCorProfilerInfo = nullptr;
        corProfilerInfo->Release();
    }
}

void StackSamplerLoop::Join()
{
    std::thread* pLoopThread = _pLoopThread;
    if (pLoopThread != nullptr)
    {
        // race condition if the Manager just terminated our thread
        try
        {
            pLoopThread->join();
        }
        catch (const std::exception&)
        {
        }

        delete pLoopThread;
        _pLoopThread = nullptr;
    }
}

void StackSamplerLoop::RequestShutdown()
{
    _shutdownRequested = true;
}

void StackSamplerLoop::MainLoop()
{
    Log::Debug("StackSamplerLoop::MainLoop started.");

    HRESULT hr = _pCorProfilerInfo->InitializeCurrentThread();
    if (hr != S_OK)
    {
        Log::Error("ICorProfilerInfo4::InitializeCurrentThread(..) on StackSamplerLoop::MainLoop's thread did not complete successfully (HRESULT=", hr, ").");
    }

    _loopThreadOsId = OpSysTools::GetThreadId();
    _pThreadsCpuManager->Map(_loopThreadOsId, ThreadName);

    while (!_shutdownRequested)
    {
        try
        {
            WaitOnePeriod();
            MainLoopIteration();
        }
        catch (const std::runtime_error& re)
        {
            Log::Error("Runtime error in StackSamplerLoop::MainLoop: ", re.what());
        }
        catch (const std::exception& ex)
        {
            Log::Error("Typed Exception in StackSamplerLoop::MainLoop: ", ex.what());
        }
        catch (...)
        {
            Log::Error("Unknown Exception in StackSamplerLoop::MainLoop.");
        }
    }

    Log::Debug("StackSamplerLoop::MainLoop has ended.");
}

void StackSamplerLoop::WaitOnePeriod(void)
{
    std::this_thread::sleep_for(SamplingPeriod);
}

void StackSamplerLoop::MainLoopIteration(void)
{
    // In each iteration, a few threads (up to MaxThreadsPerIterationForWallTime) are sampled
    // to compute wall time.
    if (_pConfiguration->IsWallTimeProfilingEnabled())
    {
        WalltimeProfilingIteration();
    }

    // When CPU profiling is enabled, most of the threads (up to MaxThreadsPerIterationForCpuTime)
    // are scanned and if they are currently running, they are sampled.
    if (_pConfiguration->IsCpuProfilingEnabled())
    {
        CpuProfilingIteration();
    }
}

void StackSamplerLoop::WalltimeProfilingIteration(void)
{
    int managedThreadsCount = _pManagedThreadList->Count();
    int sampledThreadsCount = (std::min)(managedThreadsCount, MaxThreadsPerIterationForWallTime);

    for (int i = 0; i < sampledThreadsCount && !_shutdownRequested; i++)
    {
        _targetThread = _pManagedThreadList->LoopNext(_iteratorWallTime);
        if (_targetThread != nullptr)
        {
            int64_t thisSampleTimestampNanosecs = OpSysTools::GetHighPrecisionNanoseconds();
            int64_t prevSampleTimestampNanosecs = _targetThread->SetLastSampleHighPrecisionTimestampNanoseconds(thisSampleTimestampNanosecs);
            int64_t duration = ComputeWallTime(thisSampleTimestampNanosecs, prevSampleTimestampNanosecs);

            CollectOneThreadStackSample(_targetThread, thisSampleTimestampNanosecs, duration, PROFILING_TYPE::WallTime);

            // LoopNext() calls AddRef() on the threadInfo before returning it.
            // This is because it needs to happen under the managedThreads's internal lock
            // so that a concurrently dying thread cannot delete our threadInfo while we
            // are just about to start processing it.
            _targetThread->Release();
            _targetThread = nullptr;

            // @ToDo: Investigate whether the OpSysTools::StartPreciseTimerServices(..) invocation made by
            // the StackSamplerLoopManager ctor really ensures that this yield for 1ms or less.
            // If not, we should not be yielding here.
            std::this_thread::yield();
        }
    }
}

void StackSamplerLoop::CpuProfilingIteration(void)
{
    int managedThreadsCount = _pManagedThreadList->Count();
    // TODO: as an optimization, don't scan more threads than nb logical cores
    int sampledThreadsCount = (std::min)(managedThreadsCount, MaxThreadsPerIterationForCpuTime);

    for (int i = 0; i < sampledThreadsCount && !_shutdownRequested; i++)
    {
        _targetThread = _pManagedThreadList->LoopNext(_iteratorCpuTime);
        if (_targetThread != nullptr)
        {
            // sample only if the thread is currently running on a core
            uint64_t currentConsumption = 0;
            uint64_t lastConsumption = _targetThread->GetCpuConsumptionMilliseconds();
            bool isRunning = OsSpecificApi::IsRunning(_targetThread, currentConsumption);
            // Note: it is not possible to get this information on Windows 32-bit
            //       so true is returned if this thread consumed some CPU since
            //       the last iteration
#if _WINDOWS
    #if BIT64  // nothing to do for Windows 64-bit
    #else  // Windows 32-bit
            isRunning = (lastConsumption < currentConsumption);
    #endif
#else  // nothing to do for Linux
#endif

            if (isRunning)
            {
                _targetThread->SetCpuConsumptionMilliseconds(currentConsumption);
                uint64_t cpuForSample = currentConsumption - lastConsumption;

                // we don't collect a sample for this thread is no CPU was consumed since the last check
                if (cpuForSample > 0)
                {
                    int64_t thisSampleTimestampNanosecs = OpSysTools::GetHighPrecisionNanoseconds();
                    CollectOneThreadStackSample(_targetThread, thisSampleTimestampNanosecs, cpuForSample, PROFILING_TYPE::CpuTime);
                }
            }
            // don't yield until a thread to sample is found

            // LoopNext() calls AddRef() on the threadInfo before returning it.
            // This is because it needs to happen under the managedThreads's internal lock
            // so that a concurrently dying thread cannot delete our threadInfo while we
            // are just about to start processing it.
            _targetThread->Release();
            _targetThread = nullptr;

        }
    }
}

void StackSamplerLoop::CollectOneThreadStackSample(
    ManagedThreadInfo* pThreadInfo,
    int64_t thisSampleTimestampNanosecs,
    int64_t duration,
    PROFILING_TYPE profilingType)
{
    HANDLE osThreadHandle = pThreadInfo->GetOsThreadHandle();
    if (osThreadHandle == static_cast<HANDLE>(0))
    {
        // The thread was already registered, but the OS handle is not associated yet.
        return;
    }

    // NOTE: since the StackSamplerLoop thread is not managed, it is not possible to collect ourself

    // In this section we use the uint32_t type where logically the HRESULT type would be used normally.
    // This is because we prefer avoiding HRESULT in abstractions that also apply to Linux.
    // An HRESULT is 32 bits. When a stack-collection-related API uses error codes, it will return them
    // as a uint32_t value.

    uint32_t hrCollectStack = E_FAIL;
    StackSnapshotResultBuffer* pStackSnapshotResult = nullptr;
    std::size_t countCollectedStackFrames = 0;
    {
        // The StackSamplerLoopManager may determine that the target thread is not fit for a sample collection right now.
        // This may be because it has caused some recent deadlocks.
        // In that case we skip sampling it.
        if (!_pManager->AllowStackWalk(pThreadInfo))
        {
            return;
        }

        // Prepare the collector for the next iteration. Among other things, this will pre-allocate the memory to store
        // the collected frames info. This is because we cannot allocate memory once a thread is suspended:
        // malloc() uses a lock and so if we susped a thread that was alocating, we will deadlock.
        _pStackFramesCollector->PrepareForNextCollection();


        // block used to ensure that NotifyIterationFinished gets called
        {
            // Get the timestamp of the current collection
            // /!\ Must not be called while the thread is suspended:
            // current implementation uses time function which allocates
            time_t currentUnixTimestamp = GetCurrentTimestamp();

            // Get the high-precision timestamp for this sample (this is a time-unit counter, not a real time value).
            pThreadInfo->SetLastKnownSampleUnixTimestamp(currentUnixTimestamp, thisSampleTimestampNanosecs);

            // Notify the loop manager that we are starting a stack collection, and set up a finalizer to notify the manager when we finsih it.
            // This will enable the manager to monitor if this collection freezes due to a deadlock.

            auto scopeFinalizer = CreateScopeFinalizer(
                [this] {
                    _pManager->NotifyIterationFinished();
                });

            // On Windows, we will now suspend the target thread.
            // On Linux, if we use signals, the suspension may be a no-op since signal handlers do not use explicit suspension.
            //
            // Either way, here (in the StackSamplerLoop), we pick which thread is to be targeted for stack sample collection
            // the the Collector implementation decides whether or not it needs to be suspended on the respective platform.
            bool isTargetThreadSuspended;
            if (!_pStackFramesCollector->SuspendTargetThread(pThreadInfo, &isTargetThreadSuspended))
            {
                // If there was any kind of an unexpected condition around suspending the target thread, we may not be able to stack-walk it.
                // Give up and try again during the next iteration.
                return;
            }

            // ----------- ----------- ----------- ----------- ----------- -----------
            // THE TARGET THREAD IS NOW SUSPENDED.
            // WE ARE NOW HIGHLY RESTRICTED IN THE KIND OF OPERATIONS WE MAY PERFORM.
            // E.G. WE MUST NOT CALL ANYTHING THAT MAY ALLOCATE (E.G. NO LOG STATEMENTS);
            // NO CLR LIBRARY FUNCTIONS (EXCEPT A FEW SAFE ONES);
            // ALMOST NOTHING ELSE.
            // JUST WALK THE STACK, RECORD TIMINGS, RESUME THE TARGET THREAD AND DO ALL ELSE AFTER.
            _pManager->NotifyThreadState(isTargetThreadSuspended);

            // Perform the stack walk according to bitness, OS, platform, sync/async stack-walking, etc..:
            {
                // We rely on RAII to call NotifyCollectionEnd when we get out this scope.
                auto endCollectionScope = CreateScopeFinalizer([this] { _pManager->NotifyCollectionEnd(); });

                _pManager->NotifyCollectionStart();
                pStackSnapshotResult = _pStackFramesCollector->CollectStackSample(pThreadInfo, &hrCollectStack);
            }

            // DoStackSnapshot may return a non-S_OK result even if a part of the stack was walked successfully.
            // So we will consider the walk successful, if one or more frames were collected:
            countCollectedStackFrames = pStackSnapshotResult->GetFramesCount();
            bool isStackSnapshotSuccessful = (countCollectedStackFrames > 0);

            // Keep track of how many times we sampled this thread:
            pThreadInfo->IncSnapshotsPerformedCount(isStackSnapshotSuccessful);

            if (isStackSnapshotSuccessful)
            {
                UpdateSnapshotInfos(pStackSnapshotResult, duration, currentUnixTimestamp);
                pStackSnapshotResult->DetermineAppDomain(pThreadInfo->GetClrThreadId(), _pCorProfilerInfo);
            }

            // If we got here, then either target thread == sampler thread (we are sampling the current thread),
            // or we have suspended the target thread.
            // We must now resume the target thread.
            uint32_t hr;

            // TODO: no need to call it if isTargetThreadSuspended is TRUE
            _pStackFramesCollector->ResumeTargetThreadIfRequired(pThreadInfo, isTargetThreadSuspended, &hr);
            if (FAILED(hr))
            {
                // So SuspendThread(..) worked and ResumeThread(..) did not. This needs investiation.
                // We should not be logging when the target thread is suspended, but we are about to move on and do all sorts of things,
                // so logging this is the least we can do.
                // So far we have not actually encountered this situation in practice.
                // Consider: Should we assume that something is badly wrong and stop profiling altogether to avoid affecting the user app?
                Log::Error("SuspendThread(..) worked, but ResumeThread(..) failed.");
            }

            // THE TARGET THREAD IS NOW RESUMED.
            // WE ARE NOW FREE TO CALL NORMAL APIS THAT ARE GENERALLY SAFE TO CALL FROM A PROFILER.
            // ----------- ----------- ----------- ----------- ----------- -----------
        } // _pManager->AllowStackWalk(..)

    } // SemaphoreScope guardedLock(pThreadInfo->GetStackWalkLock())

    UpdateStatistics(hrCollectStack, countCollectedStackFrames);

    LogEncounteredStackSnapshotResultStatistics(thisSampleTimestampNanosecs);

    // Store stack-walk results into the results buffer:
    PersistStackSnapshotResults(pStackSnapshotResult, pThreadInfo, profilingType);
}

void StackSamplerLoop::UpdateStatistics(HRESULT hrCollectStack, std::size_t countCollectedStackFrames)
{
    // Counts stats on how often we encounter certain results.
    // For now we only print it to the debug log.
    // However, summary statistics of this are a good candidate for global telemetry in the future.

    // All of these counters will cycle over time, especially _totalStacksCollected.
    // For current Debug Log purposes this does not matter.
    // For future global telemetry we may need to find some way to put it in the context of overall runtime
    // to interpret correctly.
    uint64_t& encounteredStackSnapshotHrCount = _encounteredStackSnapshotHRs[hrCollectStack];
    ++encounteredStackSnapshotHrCount;

    uint64_t& encounteredStackSnapshotDepthCount = _encounteredStackSnapshotDepths[countCollectedStackFrames];
    ++encounteredStackSnapshotDepthCount;

    ++_totalStacksCollectedCount;
}

void StackSamplerLoop::UpdateSnapshotInfos(StackSnapshotResultBuffer* const pStackSnapshotResult, int64_t representedDurationNanosecs, time_t currentUnixTimestamp)
{
    pStackSnapshotResult->SetRepresentedDurationNanoseconds(representedDurationNanosecs);
    pStackSnapshotResult->SetUnixTimeUtc(static_cast<int64_t>(currentUnixTimestamp));
}

time_t StackSamplerLoop::GetCurrentTimestamp()
{
    // /!\ time function allocates so we *MUST* not call it while the thread is suspended

    time_t currentUnixTimestamp;
    time(&currentUnixTimestamp);

    if (currentUnixTimestamp == static_cast<time_t>(-1))
    {
        currentUnixTimestamp = 0;
    }

    return currentUnixTimestamp;
}

int64_t StackSamplerLoop::ComputeWallTime(int64_t currentTimestampNs, int64_t prevTimestampNs)
{
    if (prevTimestampNs == 0)
    {
        // prevTimestampNs = 0 means that it is the first time the wall time is computed for a given thread
        // --> at least one sampling period has elapsed
        return static_cast<int64_t>(SamplingPeriod.count());
    }

    if (prevTimestampNs > 0)
    {
        auto durationNs = currentTimestampNs - prevTimestampNs;
        return (std::max)(static_cast<int64_t>(0), durationNs);
    }
    else
    {
        // this should never happen
        // count at least one sampling period
        return static_cast<int64_t>(SamplingPeriod.count());
    }
}

void StackSamplerLoop::LogEncounteredStackSnapshotResultStatistics(int64_t thisSampleTimestampNanosecs, bool useStdOutInsteadOfLog)
{
    if ((_lastStackSnapshotResultsStats_LogTimestampNS != 0) && (thisSampleTimestampNanosecs - _lastStackSnapshotResultsStats_LogTimestampNS < StackSamplerLoop_StackSnapshotResultsStats_LogPeriodNS))
    {
        return;
    }

    if (!(useStdOutInsteadOfLog || Log::IsDebugEnabled()))
    {
        return;
    }

    // We avoid printing the '%' char because every time we pipe the string through a formatter, it gets reduced.
    constexpr const char* PercentWord = "Percent";

    // Log total stacks collected:
    uint64_t timeSinceLastLogMS = (0 == _lastStackSnapshotResultsStats_LogTimestampNS)
                                           ? 0
                                           : (thisSampleTimestampNanosecs - _lastStackSnapshotResultsStats_LogTimestampNS) / 1000000;

    _lastStackSnapshotResultsStats_LogTimestampNS = thisSampleTimestampNanosecs;

    if (useStdOutInsteadOfLog)
    {
        std::cout << "Total Collected Stacks Count: " << _totalStacksCollectedCount << ". Time since last Stack Snapshot Result-Statistic Log: " << timeSinceLastLogMS << " ms.";
    }
    else
    {
        Log::Info("Total Collected Stacks Count: ", _totalStacksCollectedCount,
                  ". Time since last Stack Snapshot Result-Statistic Log: ", timeSinceLastLogMS, " ms.");
    }

    // Order HResults by their frequency for easier-to-read output:
    std::multimap<uint64_t, HRESULT> orderedStackSnapshotHRs;
    uint64_t cumHrFreq = 0;
    std::unordered_map<HRESULT, uint64_t>::iterator iterHRs = _encounteredStackSnapshotHRs.begin();
    while (iterHRs != _encounteredStackSnapshotHRs.end())
    {
        orderedStackSnapshotHRs.insert(std::pair<uint64_t, HRESULT>(iterHRs->second, iterHRs->first));
        cumHrFreq += iterHRs->second;
        iterHRs++;
    }

    // Log Stack Collection HResult frequency distribution:
    std::string outBuff;
    auto iterOrderedHRs = orderedStackSnapshotHRs.rbegin();
    while (iterOrderedHRs != orderedStackSnapshotHRs.rend())
    {
        uint64_t freq = iterOrderedHRs->first;
        HRESULT hr = iterOrderedHRs->second;
        std::stringstream builder;
        builder << "    " << HResultConverter::ToChars(hr) << " (0x" << std::hex << hr << "): " << std::dec << freq << " (" << std::setprecision(2) << (freq * 100.0 / cumHrFreq) << " %)\n";
        outBuff.append(builder.str());
        iterOrderedHRs++;
    }

    if (useStdOutInsteadOfLog)
    {
        std::cout << "Distribution of encountered stack snapshot collection HResults: \n"
                  << outBuff.c_str();
    }
    else
    {
        Log::Info("Distribution of encountered stack snapshot collection HResults: \n", outBuff.c_str());
    }

    // Order stack depths by their frame counts for easier-to-read output:
    decltype(_encounteredStackSnapshotDepths) orderedStackSnapshotDepths;
    uint64_t cumDepthFreq = 0;
    auto iterDepths = _encounteredStackSnapshotDepths.begin();
    while (iterDepths != _encounteredStackSnapshotDepths.end())
    {
        orderedStackSnapshotDepths.insert(std::make_pair(iterDepths->first, iterDepths->second));
        cumDepthFreq += iterDepths->second;
        iterDepths++;
    }

    // Log Stack Depth frequency distribution:
    outBuff.clear();
    auto iterOrderedDepths = orderedStackSnapshotDepths.begin();
    while (iterOrderedDepths != orderedStackSnapshotDepths.end())
    {
        auto freq = iterOrderedDepths->second;
        auto depth = iterOrderedDepths->first;
        std::stringstream builder;
        builder << "    " << std::setw(4) << depth << " frames: " << freq << " \t\t(" << std::setw(5) << std::setprecision(2) << (freq * 100.0 / cumDepthFreq) << " " << PercentWord << ")\n";
        outBuff.append(builder.str());
        iterOrderedDepths++;
    }

    if (useStdOutInsteadOfLog)
    {
        std::cout << "Distribution of encountered stack snapshot frame counts: \n"
                  << outBuff.c_str();
    }
    else
    {
        Log::Info("Distribution of encountered stack snapshot frame counts: \n", outBuff.c_str());
    }
}

void StackSamplerLoop::PersistStackSnapshotResults(
    StackSnapshotResultBuffer const* pSnapshotResult,
    ManagedThreadInfo* pThreadInfo,
    PROFILING_TYPE profilingType)
{
    if (pSnapshotResult == nullptr || pSnapshotResult->GetFramesCount() == 0)
    {
        return;
    }

    if (profilingType == PROFILING_TYPE::WallTime)
    {
        // add the WallTime sample to the lipddprof pipeline
        RawWallTimeSample rawSample;
        rawSample.Timestamp = pSnapshotResult->GetUnixTimeUtc();
        rawSample.LocalRootSpanId = pSnapshotResult->GetLocalRootSpanId();
        rawSample.SpanId = pSnapshotResult->GetSpanId();
        rawSample.AppDomainId = pSnapshotResult->GetAppDomainId();
        pSnapshotResult->CopyInstructionPointers(rawSample.Stack);
        rawSample.ThreadInfo = pThreadInfo;
        pThreadInfo->AddRef();
        rawSample.Duration = pSnapshotResult->GetRepresentedDurationNanoseconds();
        _pWallTimeCollector->Add(std::move(rawSample));
    }
    else
    if (profilingType == PROFILING_TYPE::CpuTime)
    {
        // add the CPU sample to the lipddprof pipeline if needed
        RawCpuSample rawCpuSample;
        rawCpuSample.Timestamp = pSnapshotResult->GetUnixTimeUtc();
        rawCpuSample.LocalRootSpanId = pSnapshotResult->GetLocalRootSpanId();
        rawCpuSample.SpanId = pSnapshotResult->GetSpanId();
        rawCpuSample.AppDomainId = pSnapshotResult->GetAppDomainId();
        pSnapshotResult->CopyInstructionPointers(rawCpuSample.Stack);
        rawCpuSample.ThreadInfo = pThreadInfo;
        pThreadInfo->AddRef();
        rawCpuSample.Duration = pSnapshotResult->GetRepresentedDurationNanoseconds();
        _pCpuTimeCollector->Add(std::move(rawCpuSample));
    }
}
