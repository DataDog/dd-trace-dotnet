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
    IManagedThreadList* pCodeHotspotThreadList,
    ICollector<RawWallTimeSample>* pWallTimeCollector,
    ICollector<RawCpuSample>* pCpuTimeCollector,
    MetricsRegistry& metricsRegistry)
    :
    _pCorProfilerInfo{pCorProfilerInfo},
    _pConfiguration{pConfiguration},
    _pStackFramesCollector{pStackFramesCollector},
    _pManager{pManager},
    _pThreadsCpuManager{pThreadsCpuManager},
    _pManagedThreadList{pManagedThreadList},
    _pCodeHotspotsThreadList{pCodeHotspotThreadList},
    _pWallTimeCollector{pWallTimeCollector},
    _pCpuTimeCollector{pCpuTimeCollector},
    _pLoopThread{nullptr},
    _loopThreadOsId{0},
    _targetThread(nullptr),
    _iteratorWallTime{0},
    _iteratorCpuTime{0},
    _walltimeThreadsThreshold{pConfiguration->WalltimeThreadsThreshold()},
    _cpuThreadsThreshold{pConfiguration->CpuThreadsThreshold()},
    _codeHotspotsThreadsThreshold{pConfiguration->CodeHotspotsThreadsThreshold()},
    _isWalltimeEnabled{pConfiguration->IsWallTimeProfilingEnabled()},
    _isCpuEnabled{pConfiguration->IsCpuProfilingEnabled()},
    _areInternalMetricsEnabled{pConfiguration->IsInternalMetricsEnabled()}
{
    _nbCores = OsSpecificApi::GetProcessorCount();
    Log::Info("Processor cores = ", _nbCores);

    _samplingPeriod = _pConfiguration->CpuWallTimeSamplingRate();
    Log::Info("CPU and wall time sampling period = ", _samplingPeriod.count() / 1000000, " ms");
    Log::Info("Wall time sampled threads = ", _walltimeThreadsThreshold);
    Log::Info("Max CodeHotspots sampled threads = ", _codeHotspotsThreadsThreshold);
    Log::Info("Max CPU sampled threads = ", _cpuThreadsThreshold);

    _pCorProfilerInfo->AddRef();

    _iteratorWallTime = _pManagedThreadList->CreateIterator();
    _iteratorCpuTime = _pManagedThreadList->CreateIterator();
    _iteratorCodeHotspot = _pCodeHotspotsThreadList->CreateIterator();

    _pLoopThread = new std::thread(&StackSamplerLoop::MainLoop, this);
    OpSysTools::SetNativeThreadName(_pLoopThread, ThreadName);

    if(_areInternalMetricsEnabled)
    {
        _walltimeDurationMetric = metricsRegistry.GetOrRegister<MeanMaxMetric>("dotnet_internal_walltime_iterations_duration");
        _cpuDurationMetric = metricsRegistry.GetOrRegister<MeanMaxMetric>("dotnet_internal_cpu_iterations_duration");
    }
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
            OpSysTools::Sleep(_samplingPeriod);
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

void StackSamplerLoop::MainLoopIteration()
{
    int64_t timestampNanosecs1 = 0;
    int64_t timestampNanosecs2 = 0;

    // In each iteration, a few threads are sampled to compute wall time.
    if (_isWalltimeEnabled)
    {
        if (_areInternalMetricsEnabled)
        {
            timestampNanosecs1 = OpSysTools::GetHighPrecisionTimestamp();
        }

        // First we collect threads that have trace context to increase the chance to get
        CodeHotspotIteration();
        // Then we collect threads that do not have trace context
        WalltimeProfilingIteration();

        if (_areInternalMetricsEnabled)
        {
            timestampNanosecs2 = OpSysTools::GetHighPrecisionTimestamp();
            _walltimeDurationMetric->Add(static_cast<double>(timestampNanosecs2 - timestampNanosecs1));
        }
    }

    // When CPU profiling is enabled, most of the threads are scanned
    // and if they are currently running, they are sampled.
    if (_isCpuEnabled)
    {
        CpuProfilingIteration();

        if (_areInternalMetricsEnabled)
        {
            timestampNanosecs1 = OpSysTools::GetHighPrecisionTimestamp();
            _cpuDurationMetric->Add(static_cast<double>(timestampNanosecs1 - timestampNanosecs2));
        }
    }
}

void StackSamplerLoop::WalltimeProfilingIteration()
{
    int32_t managedThreadsCount = _pManagedThreadList->Count();
    int32_t sampledThreadsCount = (std::min)(managedThreadsCount, _walltimeThreadsThreshold);

    int32_t i = 0;

    ManagedThreadInfo* firstThread = nullptr;

    do
    {
        _targetThread = _pManagedThreadList->LoopNext(_iteratorWallTime);

        // either the list is empty or iterator is not in the array range
        // so prefer bailing out
        if (_targetThread == nullptr)
        {
            break;
        }

        if (firstThread == _targetThread.get())
        {
            _targetThread.reset();
            break;
        }

        if (firstThread == nullptr)
        {
            firstThread = _targetThread.get();
        }

        // skip thread if it has a trace context
        if (_targetThread->HasTraceContext())
        {
            _targetThread.reset();
            continue;
        }

        int64_t thisSampleTimestampNanosecs = OpSysTools::GetHighPrecisionTimestamp();
        int64_t prevSampleTimestampNanosecs = _targetThread->SetLastSampleHighPrecisionTimestampNanoseconds(thisSampleTimestampNanosecs);
        int64_t duration = ComputeWallTime(thisSampleTimestampNanosecs, prevSampleTimestampNanosecs);

        CollectOneThreadStackSample(_targetThread, thisSampleTimestampNanosecs, duration, PROFILING_TYPE::WallTime);

        _targetThread.reset();
        i++;

    } while (i < sampledThreadsCount && !_shutdownRequested);

}

void StackSamplerLoop::CpuProfilingIteration()
{
    uint32_t sampledThreads = 0;
    int32_t managedThreadsCount = _pManagedThreadList->Count();
    int32_t sampledThreadsCount = (std::min)(managedThreadsCount, _cpuThreadsThreshold);

    for (int32_t i = 0; i < sampledThreadsCount && !_shutdownRequested; i++)
    {
        _targetThread = _pManagedThreadList->LoopNext(_iteratorCpuTime);
        if (_targetThread != nullptr)
        {
            // detect Windows API call failure
            bool failure = false;

            // sample only if the thread is currently running on a core
            uint64_t currentConsumption = 0;
            uint64_t lastConsumption = _targetThread->GetCpuConsumptionMilliseconds();
            bool isRunning = OsSpecificApi::IsRunning(_targetThread.get(), currentConsumption, failure);
            // Note: it is not possible to get this information on Windows 32-bit or in some cases in 64-bit
            //       so isRunning should be true if this thread consumed some CPU since the last iteration
#if _WINDOWS
    #if BIT64  // Windows 64-bit
            if (failure)
            {
                isRunning = (lastConsumption < currentConsumption);
            }
    #else  // Windows 32-bit
            isRunning = (lastConsumption < currentConsumption);
    #endif
#else  // nothing to do for Linux
#endif

            if (isRunning)
            {
                uint64_t cpuForSample = currentConsumption - lastConsumption;

                // we don't collect a sample for this thread is no CPU was consumed since the last check
                if (cpuForSample > 0)
                {
                    int64_t lastCpuTimestamp = _targetThread->GetCpuTimestamp();
                    int64_t thisSampleTimestampNanosecs = OpSysTools::GetHighPrecisionTimestamp();

                    // detect overlapping CPU usage
                    if ((int64_t)(lastCpuTimestamp + cpuForSample * 1000000) > thisSampleTimestampNanosecs)
                    {
                        int64_t cpuOverlap = (lastCpuTimestamp + cpuForSample * 1000000 - thisSampleTimestampNanosecs) / 1000000;
#ifndef NDEBUG
                        // TODO: uncomment when debugging this issue
                        // Log::Warn("Overlapping CPU samples off ", cpuOverlap, " ms (", currentConsumption, " - ", lastConsumption, ")");
#endif
                        // ensure that we don't overlap
                        // -> only the largest possibly CPU consumption is accounted = diff between the 2 timestamps
                        cpuForSample = (thisSampleTimestampNanosecs - lastCpuTimestamp - 1000) / 1000000; // removing 1 microsecond to be sure
                    }
                    _targetThread->SetCpuConsumptionMilliseconds(currentConsumption, thisSampleTimestampNanosecs);
                    CollectOneThreadStackSample(_targetThread, thisSampleTimestampNanosecs, cpuForSample, PROFILING_TYPE::CpuTime);

                    // don't scan more threads than nb logical cores
                    sampledThreads++;
                    if (sampledThreads >= _nbCores)
                    {
                        break;
                    }
                }
            }
            _targetThread.reset();
        }
    }
}

void StackSamplerLoop::CodeHotspotIteration()
{
    int32_t managedThreadsCount = _pManagedThreadList->Count();
    int32_t sampledThreadsCount = (std::min)(managedThreadsCount, _codeHotspotsThreadsThreshold);

    int32_t i = 0;
    ManagedThreadInfo* firstThread = nullptr;

    do
    {
        _targetThread = _pCodeHotspotsThreadList->LoopNext(_iteratorCodeHotspot);

        // either the list is empty or iterator is not in the array range, so there is a bug
        // so prefer bailing out
        if (_targetThread == nullptr)
        {
            break;
        }

        // keep track of the first seen thread, to avoid infinite loop
        if (firstThread == _targetThread.get())
        {
            _targetThread.reset();
            break;
        }

        if (firstThread == nullptr)
        {
            firstThread = _targetThread.get();
        }

        // skip if it has no trace context
        if (!_targetThread->HasTraceContext())
        {
            _targetThread.reset();
            continue;
        }

        int64_t thisSampleTimestampNanosecs = OpSysTools::GetHighPrecisionTimestamp();
        int64_t prevSampleTimestampNanosecs = _targetThread->SetLastSampleHighPrecisionTimestampNanoseconds(thisSampleTimestampNanosecs);
        int64_t duration = ComputeWallTime(thisSampleTimestampNanosecs, prevSampleTimestampNanosecs);

        CollectOneThreadStackSample(_targetThread, thisSampleTimestampNanosecs, duration, PROFILING_TYPE::WallTime);

        _targetThread.reset();
        i++;
    } while (i < sampledThreadsCount && !_shutdownRequested);
}

void StackSamplerLoop::CollectOneThreadStackSample(
    std::shared_ptr<ManagedThreadInfo>& pThreadInfo,
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

            on_leave { _pManager->NotifyIterationFinished(); };

            // On Windows, we will now suspend the target thread.
            // On Linux, if we use signals, the suspension may be a no-op since signal handlers do not use explicit suspension.
            //
            // Either way, here (in the StackSamplerLoop), we pick which thread is to be targeted for stack sample collection
            // the the Collector implementation decides whether or not it needs to be suspended on the respective platform.
            bool isTargetThreadSuspended;
            if (!_pStackFramesCollector->SuspendTargetThread(pThreadInfo.get(), &isTargetThreadSuspended))
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
                on_leave { _pManager->NotifyCollectionEnd(); };

                _pManager->NotifyCollectionStart();
                pStackSnapshotResult = _pStackFramesCollector->CollectStackSample(pThreadInfo.get(), &hrCollectStack);
            }

            // DoStackSnapshot may return a non-S_OK result even if a part of the stack was walked successfully.
            // So we will consider the walk successful, if one or more frames were collected:
            countCollectedStackFrames = pStackSnapshotResult->GetFramesCount();
            bool isStackSnapshotSuccessful = (countCollectedStackFrames > 0);

            // Keep track of how many times we sampled this thread:
            pThreadInfo->IncSnapshotsPerformedCount(isStackSnapshotSuccessful);

            if (isStackSnapshotSuccessful)
            {
                UpdateSnapshotInfos(pStackSnapshotResult, duration, thisSampleTimestampNanosecs);
                pStackSnapshotResult->DetermineAppDomain(pThreadInfo->GetClrThreadId(), _pCorProfilerInfo);
            }

            // If we got here, then either target thread == sampler thread (we are sampling the current thread),
            // or we have suspended the target thread.
            // We must now resume the target thread.
            uint32_t hr;

            // TODO: no need to call it if isTargetThreadSuspended is TRUE
            _pStackFramesCollector->ResumeTargetThreadIfRequired(pThreadInfo.get(), isTargetThreadSuspended, &hr);
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
        return static_cast<int64_t>(_samplingPeriod.count());
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
        return static_cast<int64_t>(_samplingPeriod.count());
    }
}

void StackSamplerLoop::PersistStackSnapshotResults(
    StackSnapshotResultBuffer const* pSnapshotResult,
    std::shared_ptr<ManagedThreadInfo>& pThreadInfo,
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
        rawCpuSample.Duration = pSnapshotResult->GetRepresentedDurationNanoseconds();
        _pCpuTimeCollector->Add(std::move(rawCpuSample));
    }
}
