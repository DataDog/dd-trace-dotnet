// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include <chrono>
#include <inttypes.h>
#include <map>
#include <memory>
#include <stdio.h>

#include "HResultConverter.h"
#include "Log.h"
#include "ManagedThreadInfo.h"
#include "ManagedThreadList.h"
#include "OpSysTools.h"
#include "ScopeFinalizer.h"
#include "StackFrameInfo.h"
#include "StackFramesCollectorBase.h"
#include "StackSamplerLoop.h"
#include "StackSamplerLoopManager.h"
#include "StackSnapshotsBufferManager.h"
#include "SymbolsResolver.h"
#include "ThreadsCpuManager.h"

#include "shared/src/native-src/string.h"

// Configuration constants:
using namespace std::chrono_literals;
constexpr std::chrono::nanoseconds SamplingPeriod = 9ms;
constexpr std::int32_t SampledThreadsPerIteration = 5;
constexpr const WCHAR* StackSamplerLoop_ThreadName = WStr("DD.Profiler.StackSamplerLoop.Thread");

#ifdef NDEBUG
// Release build logs collection stats every 30 mins:
constexpr std::uint64_t StackSamplerLoop_StackSnapshotResultsStats_LogPeriodNS = (30 * 60 * 1000000000ull);
#else
// Debug build build logs collection stats every 1 mins:
constexpr std::uint64_t StackSamplerLoop_StackSnapshotResultsStats_LogPeriodNS = (1 * 60 * 1000000000ull);
#endif

StackSamplerLoop::StackSamplerLoop(ICorProfilerInfo4* pCorProfilerInfo, StackFramesCollectorBase* pStackFramesCollector, StackSamplerLoopManager* pManager) :
    _pCorProfilerInfo{pCorProfilerInfo},
    _pStackFramesCollector{pStackFramesCollector},
    _pManager{pManager},
    _pLoopThread{nullptr},
    _loopThreadOsId{0},
    _targetThread(nullptr)
{
    _pCorProfilerInfo->AddRef();

    _pLoopThread = new std::thread(&StackSamplerLoop::MainLoop, this);
    OpSysTools::SetNativeThreadName(_pLoopThread, StackSamplerLoop_ThreadName);
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
    ThreadsCpuManager::GetSingletonInstance()->Map(_loopThreadOsId, StackSamplerLoop_ThreadName);

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
    ManagedThreadList* pManagedThreads = ManagedThreadList::GetSingletonInstance();

    // The true count of managed thread can change concurrently.
    // If it stays above SampleThreadsPerIteration, it is irrelevant for the code here.
    // If it falls under SampleThreadsPerIteration, we will simply sample some threads
    // more than once in this iteration, which is OK.
    // If it falls to zero, we are still OK:
    // it is very rare and we will simply loop up to SampleThreadsPerIteration doing nothing.
    int managedThreadsCount = pManagedThreads->Count();
    int thisIterationCount = (std::min)(managedThreadsCount, SampledThreadsPerIteration);

    for (int i = 0; i < thisIterationCount && false == _shutdownRequested; i++)
    {
        _targetThread = pManagedThreads->LoopNext();
        if (_targetThread != nullptr)
        {
            CollectOneThreadStackSample(_targetThread);

            // LoopNext() calls AddRef() on the threadInfo before returning it.
            // This is because it needs to happen under the managedThreads's internal lock
            // so that a concurrently dying thread cannot delete our threadInfo while we
            // are just about to start processing it.
            _targetThread->Release();
            _targetThread = nullptr;

            // @ToDo: Investigate whether the OpSysTools::StartPreciseTimerServices(..) invocation made by
            // the StackSamplerLoopManager ctor really ensures that thie yield for 1ms or less.
            // If not, we should not be yielding here.
            std::this_thread::yield();
        }
    }
}

void StackSamplerLoop::CollectOneThreadStackSample(ManagedThreadInfo* pThreadInfo)
{
    HANDLE osThreadHandle = pThreadInfo->GetOsThreadHandle();
    if (osThreadHandle == static_cast<HANDLE>(0))
    {

        // It may be that the thread was already registered, but we did not yet initialize the handle.
        // In that case we cannot process it. We will just sample it next time.
        return;
    }

    // NOTE: since this is a native thread, it is not possible to collect ourself

    // In this section we use the uint32_t type where logically the HRESULT type would be used normally.
    // This is because we prefer avoiding HRESULT in abstractions that also apply to Linux.
    // An HRESULT is 32 bits. When a stack-collection-related API uses error codes, it will return them
    // as a uint32_t value.

    uint32_t hrCollectStack = E_FAIL;
    StackSnapshotResultBuffer* pStackSnapshotResult = nullptr;
    std::uint16_t countCollectedStackFrames = 0;
    std::int64_t thisSampleTimestampNanosecs = 0;
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


        // block used to ensure that NotifyIteationFinished gets called
        {
            // Get the timestamp of the current collection
            // /!\ Must not be called while the thread is suspended:
            // current implementation uses time function which allocates
            time_t currentUnixTimestamp = GetCurrentTimestamp();

            // Get the high-precision timestamp for this sample (this is a time-unit counter, not a real time value).
            thisSampleTimestampNanosecs = OpSysTools::GetHighPrecisionNanoseconds();
            std::int64_t prevSampleTimestampNanosecs = pThreadInfo->SetLastSampleHighPrecisionTimestampNanoseconds(thisSampleTimestampNanosecs);
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
                std::int64_t wallTime = ComputeWallTime(thisSampleTimestampNanosecs, prevSampleTimestampNanosecs);
                UpdateSnapshotInfos(pStackSnapshotResult, wallTime, currentUnixTimestamp);
                DetermineAppDomain(pThreadInfo->GetClrThreadId(), pStackSnapshotResult);
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

    // Now that the target thread is resumed, we can resolve the code kind for any frames with undetermined stack-frame code kinds:
    DetermineSampledStackFrameCodeKinds(pStackSnapshotResult);

    // Store stack-walk results into the results buffer:
    PersistStackSnapshotResults(pStackSnapshotResult, pThreadInfo);
}

void StackSamplerLoop::UpdateStatistics(HRESULT hrCollectStack, std::uint16_t countCollectedStackFrames)
{
    // Counts stats on how often we encounter certain results.
    // For now we only print it to the debug log.
    // However, summary statistics of this are a good candidate for global telemetry in the future.

    // All of these counters will cycle over time, especially _totalStacksCollected.
    // For current Debug Log purposes this does not matter.
    // For future global telemetry we may need to find some way to put it in the context of overall runtime
    // to interpret correctly.
    std::uint64_t& encounteredStackSnapshotHrCount = _encounteredStackSnapshotHRs[hrCollectStack];
    ++encounteredStackSnapshotHrCount;

    std::uint64_t& encounteredStackSnapshotDepthCount = _encounteredStackSnapshotDepths[countCollectedStackFrames];
    ++encounteredStackSnapshotDepthCount;

    ++_totalStacksCollectedCount;
}

void StackSamplerLoop::UpdateSnapshotInfos(StackSnapshotResultBuffer* const pStackSnapshotResult, std::int64_t representedDurationNanosecs, time_t currentUnixTimestamp)
{
    pStackSnapshotResult->SetRepresentedDurationNanoseconds(representedDurationNanosecs);
    pStackSnapshotResult->SetUnixTimeUtc(static_cast<std::int64_t>(currentUnixTimestamp));
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

std::int64_t StackSamplerLoop::ComputeWallTime(std::int64_t currentTimestampNs, std::int64_t prevTimestampNs)
{
    if (prevTimestampNs == 0)
    {
        // prevTimestampNs = 0 means that it is the first time the wall time is computed for a given thread
        // --> at least one sampling period has elapsed
        return static_cast<std::int64_t>(SamplingPeriod.count());
    }

    if (prevTimestampNs > 0)
    {
        auto durationNs = currentTimestampNs - prevTimestampNs;
        return (std::max)(static_cast<std::int64_t>(0), durationNs);
    }
    else
    {
        // this should never happen
        // count at least one sampling period
        return static_cast<std::int64_t>(SamplingPeriod.count());
    }
}

void StackSamplerLoop::DetermineAppDomain(ThreadID threadId, StackSnapshotResultBuffer* const pStackSnapshotResult)
{
    // Determine the AppDomain currently running the sampled thread:
    //
    // (Note: On Windows, the target thread is still suspended and the AddDomain ID will be correct.
    // However, on Linux the signal handler that performed the stack walk has finished and the target
    // thread is making progress again.
    // So, it is possible that since we walked the stack, the thread's AppDomain changed and the AppDomain ID we
    // read here does not correspond to the stack sample. In practice we expect this to occur very rarely,
    // so we accept this for now.
    // If, however, this is observed frequently enough to present a problem, we will need to move the AppDomain
    // ID read logic into _pStackFramesCollector->CollectStackSample(). Collectors that suspend the target thread
    // will be able to read the ID at any time, but non-suspending collectors (e.g. Linux) will need to do it from
    // within the signal handler. An example for this is the
    // StackFramesCollectorBase::TryApplyTraceContextDataFromCurrentCollectionThreadToSnapshot() API which exists
    // to address the same synchronization issue with TraceContextTracking-related data.
    // There is an additioal complexity with the AppDomain case, because it is likely not safe to call
    // _pCorProfilerInfo->GetThreadAppDomain() from the collector's signal handler directly (needs to be investigated).
    // To address this, we will need to do it via a SynchronousOffThreadWorkerBase-based mechanism, similar to how
    // the SymbolsResolver uses a Worker and synchronously waits for results to avoid calling
    // symbol resolution APIs on a CLR thread.)
    AppDomainID appDomainId;
    HRESULT hr = _pCorProfilerInfo->GetThreadAppDomain(threadId, &appDomainId);
    if (SUCCEEDED(hr))
    {
        pStackSnapshotResult->SetAppDomainId(appDomainId);
    }
}

void StackSamplerLoop::DetermineSampledStackFrameCodeKinds(StackSnapshotResultBuffer* pStackSnapshotResult)
{
    // Scan the collected stack frames and attempt to resolve the stack-frame code kind for each of the frames.
    std::uint16_t countCollectedStackFrames = (pStackSnapshotResult != nullptr)
                                                  ? pStackSnapshotResult->GetFramesCount()
                                                  : 0;

    for (std::uint16_t i = 0; i < countCollectedStackFrames; i++)
    {
        StackSnapshotResultFrameInfo& frame = pStackSnapshotResult->GetFrameAtIndex(i);
        if (frame.GetCodeKind() == StackFrameCodeKind::NotDetermined)
        {
            // Try to resolve the native IP to the CLR Function Id.
            // We use ICorProfilerInfo::GetFunctionFromIP() and NOT ICorProfilerInfo4::GetFunctionFromIP2() for this,
            // because GetFunctionFromIP2() can trigger a GC and the current code/thread location is not a good place for a GC.
            // See https://docs.microsoft.com/en-us/dotnet/framework/unmanaged-api/profiling/icorprofilerinfo4-getfunctionfromip2-method#remarks
            // and https://docs.microsoft.com/en-us/dotnet/framework/unmanaged-api/profiling/corprof-e-unsupported-call-sequence-hresult
            UINT_PTR nativeIP = frame.GetNativeIP();
            LPCBYTE nativeIPPtr = reinterpret_cast<const BYTE*>(nativeIP);
            FunctionID clrFunctionId;
            HRESULT hr = _pCorProfilerInfo->GetFunctionFromIP(nativeIPPtr, &clrFunctionId);

            // If GetFunctionFromIP(..) succeeded AND it gave us a valid function ID, we should be able to resolve it to a managed symbol later.
            // Otherwise, all we know is that we do not have a symbol-resolvable managed frame.
            // The frame may be ClrNative, UserNative, Kernel or perhaps something else?
            // If the future we will add some form of native symbol resolution. For now, we just call it UnknownNative and move on.
            if (SUCCEEDED(hr) && clrFunctionId > 0)
            {
                frame.Set(StackFrameCodeKind::ClrManaged, clrFunctionId, nativeIP, 0);
            }
            else
            {
                // For now, for the generic 'UnknownNative' case we will resolve the IP to the moduleHandle.
                // During symbol resolution, we will use GetModuleBaseNameW(..) to get the base name of the module.
                // In the future, we can consider using SymFromAddr(..) instead, which may give us additional detail.
                // https://docs.microsoft.com/en-us/windows/win32/api/dbghelp/nf-dbghelp-symfromaddr

                // We are doing the GetModuleHandleEx(..) now rather than later when the results buffer is processed
                // in managed code, in order to avoid PInvoke overhead for symbol-resolution of every single native frame.
                // To avoid such PInvokes, the managed engine keeps a cache-table keyed by whatever we use as the compact
                // representation of StackSnapshotResultFrameInfo with codeKind = UnknownNative. Such a table keyed
                // by IP would be way too large. Using module handles allows keeping a cache in
                // a (moduleHandle)->ModuleBaseName table.
                // If and when we switch to using SymFromAddr(..) or similar we will need to rethink this.
                std::uint64_t moduleHandle = 0;
                if (!OpSysTools::GetModuleHandleFromInstructionPointer(
                        reinterpret_cast<void*>(static_cast<std::uintptr_t>(nativeIP)),
                        &moduleHandle
                        )
                   )
                {
                    frame.Set(StackFrameCodeKind::UnknownNative, 0, nativeIP, 0);
                }
                else
                {
                    // Can we say something more specific than 'UnknownNative'?
                    // I.e., can we tell between 'ClrNative', 'UserNative' and 'Kernel'?
                    frame.Set(StackFrameCodeKind::UnknownNative, 0, nativeIP, moduleHandle);
                }
            }
        }
    }
}

void StackSamplerLoop::LogEncounteredStackSnapshotResultStatistics(std::int64_t thisSampleTimestampNanosecs, bool useStdOutInsteadOfLog)
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

    constexpr std::size_t MaxStatsLineSize = 200;
    char statsLineBuffer[MaxStatsLineSize];

    // Log total stacks collected:
    std::uint64_t timeSinceLastLogMS = (0 == _lastStackSnapshotResultsStats_LogTimestampNS)
                                           ? 0
                                           : (thisSampleTimestampNanosecs - _lastStackSnapshotResultsStats_LogTimestampNS) / 1000000;

    _lastStackSnapshotResultsStats_LogTimestampNS = thisSampleTimestampNanosecs;

    if (useStdOutInsteadOfLog)
    {
        printf("Total Collected Stacks Count: %I64d. Time since last Stack Snapshot Result-Statistic Log: %I64d ms.",
               _totalStacksCollectedCount, timeSinceLastLogMS);
    }
    else
    {
        Log::Info("Total Collected Stacks Count: ", _totalStacksCollectedCount,
                  ". Time since last Stack Snapshot Result-Statistic Log: ", timeSinceLastLogMS, " ms.");
    }

    // Order HResults by their frequency for easier-to-read output:
    std::multimap<std::uint64_t, HRESULT> orderedStackSnapshotHRs;
    std::uint64_t cumHrFreq = 0;
    std::unordered_map<HRESULT, std::uint64_t>::iterator iterHRs = _encounteredStackSnapshotHRs.begin();
    while (iterHRs != _encounteredStackSnapshotHRs.end())
    {
        orderedStackSnapshotHRs.insert(std::pair<std::uint64_t, HRESULT>(iterHRs->second, iterHRs->first));
        cumHrFreq += iterHRs->second;
        iterHRs++;
    }

    // Log Stack Collection HResult frequency distrubution:

    std::string outBuff;
    std::multimap<std::uint64_t, HRESULT>::reverse_iterator iterOrderedHRs = orderedStackSnapshotHRs.rbegin();
    while (iterOrderedHRs != orderedStackSnapshotHRs.rend())
    {
        std::uint64_t freq = iterOrderedHRs->first;
        HRESULT hr = iterOrderedHRs->second;
        snprintf(statsLineBuffer,
                 MaxStatsLineSize,
                 "    %s (0x%lx): %I64d (%.2f %s)\n",
                 HResultConverter::ToChars(hr),
                 hr,
                 freq,
                 freq * 100.0 / cumHrFreq,
                 PercentWord);
        outBuff.append(statsLineBuffer);
        iterOrderedHRs++;
    }

    if (useStdOutInsteadOfLog)
    {
        printf("Distribution of encountered stack snapshot collection HResults: \n%s", outBuff.c_str());
    }
    else
    {
        Log::Info("Distribution of encountered stack snapshot collection HResults: \n", outBuff.c_str());
    }

    // Order stack depths by their frame counts for easier-to-read output:
    std::multimap<std::uint16_t, std::uint64_t> orderedStackSnapshotDepths;
    std::uint64_t cumDepthFreq = 0;
    std::unordered_map<std::uint16_t, std::uint64_t>::iterator iterDepths = _encounteredStackSnapshotDepths.begin();
    while (iterDepths != _encounteredStackSnapshotDepths.end())
    {
        orderedStackSnapshotDepths.insert(std::pair<std::uint16_t, std::uint64_t>(iterDepths->first, iterDepths->second));
        cumDepthFreq += iterDepths->second;
        iterDepths++;
    }

    // Log Stack Depth frequency distribution:
    outBuff.clear();
    std::multimap<std::uint16_t, std::uint64_t>::iterator iterOrderedDepths = orderedStackSnapshotDepths.begin();
    while (iterOrderedDepths != orderedStackSnapshotDepths.end())
    {
        std::uint64_t freq = iterOrderedDepths->second;
        std::uint16_t depth = iterOrderedDepths->first;
        snprintf(statsLineBuffer,
                 MaxStatsLineSize,
                 "    %4d frames: %I64d \t\t(%05.2f %s)\n",
                 depth,
                 freq,
                 freq * 100.0 / cumDepthFreq,
                 PercentWord);
        outBuff.append(statsLineBuffer);
        iterOrderedDepths++;
    }

    if (useStdOutInsteadOfLog)
    {
        printf("Distribution of encountered stack snapshot frame counts: \n%s", outBuff.c_str());
    }
    else
    {
        Log::Info("Distribution of encountered stack snapshot frame counts: \n", outBuff.c_str());
    }
}

void StackSamplerLoop::PersistStackSnapshotResults(StackSnapshotResultBuffer const* pSnapshotResult, ManagedThreadInfo* pThreadInfo)
{
    if (pSnapshotResult == nullptr || pSnapshotResult->GetFramesCount() == 0)
    {
        return;
    }

    StackSnapshotsBufferManager* stackSnapshotsBufferManager = StackSnapshotsBufferManager::GetSingletonInstance();
    stackSnapshotsBufferManager->Add(pSnapshotResult, pThreadInfo);
}

void StackSamplerLoop::PrintStackSnapshotResultsForDebug(StackSnapshotResultBuffer const* pStackSnapshotResult,
                                                         ManagedThreadInfo* pThreadInfo,
                                                         std::int64_t thisSampleTimestampNanosecs)
{
    // Print fome info or full details for EACH STACK
    // (very slow, set to True only if really required):
    constexpr bool PrintInfoForEachStack = false;
    constexpr bool PrintDetailsForEachStack = false;

    std::uint32_t countCollectedStackFrames = (pStackSnapshotResult != nullptr)
                                                  ? pStackSnapshotResult->GetFramesCount()
                                                  : 0;

    // Nothing to do if no data collected:
    if (countCollectedStackFrames == 0)
    {
        return;
    }

    // Resolve symbols of the collected stack:
    SymbolsResolver* symbolsResolver = SymbolsResolver::GetSingletonInstance();
    StackFrameInfo** ppStackFrames = new StackFrameInfo*[static_cast<size_t>(countCollectedStackFrames) + 1];
    ppStackFrames[countCollectedStackFrames] = nullptr; // null terminated array of pointers to StackFrameInfo
    bool hasManagedFrames = false;

    for (std::uint32_t stackFrameIndex = 0; stackFrameIndex < countCollectedStackFrames; stackFrameIndex++)
    {
        const StackSnapshotResultFrameInfo& currResultInfo = pStackSnapshotResult->GetFrameAtIndex(stackFrameIndex);
        StackFrameInfo** ppCurrFrameResolvedInfo = (ppStackFrames + stackFrameIndex);

        symbolsResolver->ResolveStackFrameSymbols(currResultInfo, ppCurrFrameResolvedInfo);
        hasManagedFrames = hasManagedFrames || (*ppCurrFrameResolvedInfo)->GetCodeKind() == StackFrameCodeKind::ClrManaged;
    }

    // Build a string representing the collected stack (and optionally some info in the process):
    const StackFrameInfo** frame = const_cast<const StackFrameInfo**>(ppStackFrames);

    if (PrintInfoForEachStack)
    {
        std::uint64_t clrThreadId = pThreadInfo->GetClrThreadId();
        std::uint64_t osThreadId = pThreadInfo->GetOsThreadId();
        printf("\n*** Stack for ClrThreadId=%I64x; OsThreadId=%I64u%s",
               clrThreadId,
               osThreadId,
               (*frame == nullptr) ? " has 0 frames.\n" : ":\n");
    }

    shared::WSTRING outBuffStack;
    while (*frame != nullptr)
    {
        shared::WSTRING outBuffFrame;
        (*frame)->ToDisplayString(&outBuffFrame);
        outBuffStack.append(outBuffFrame);
        outBuffStack.append(WStr("\n"));
        frame++;
    }

    if (PrintDetailsForEachStack)
    {
#ifdef _WINDOWS
        wprintf(WStr("%s"), outBuffStack.c_str());
#else
        printf("%s", shared::ToString(outBuffStack).c_str());
#endif
    }

    // Print info about the stacks collected so far:
    // (print only at a certain granularity to avoid flooding the output)
    if (outBuffStack.length() > 0)
    {
        std::uint64_t& encounteredCount = _encounteredStackCountsForDebug[outBuffStack];
        ++encounteredCount;

        std::uint64_t displayGranularity = 100000;
        if (encounteredCount < 3)
        {
            displayGranularity = 1;
        }
        else if (encounteredCount < 10000)
        {
            displayGranularity = 1000;
        }
        else if (encounteredCount < 100000)
        {
            displayGranularity = 10000;
        }

        if (encounteredCount % displayGranularity == 0)
        {

            LogEncounteredStackSnapshotResultStatistics(thisSampleTimestampNanosecs, true);

            printf("\n");
            printf("*** Total number of DISTINCT stack snapshots captured so far: %zd.\n", _encounteredStackCountsForDebug.size());
            printf("*** The latest stack snapshot was captured on ClrThreadId=%" PRIxPTR " / OsThreadId=%lu."
                   " This stack was seen a total on %lld times on all threads.\n",
                   pThreadInfo->GetClrThreadId(),
                   pThreadInfo->GetOsThreadId(),
                   encounteredCount);

#ifdef _WINDOWS
            wprintf(WStr("%s\n"), outBuffStack.c_str());
#else
            printf("%s\n", shared::ToString(outBuffStack).c_str());
#endif
        }
    }

    // Release the stack frames symbols data:

    StackFrameInfo** stackFrameInfo = ppStackFrames;
    while (*stackFrameInfo != nullptr)
    {
        (*stackFrameInfo)->Release();
        stackFrameInfo++;
    }

    delete[] ppStackFrames;
}
