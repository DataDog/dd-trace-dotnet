// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "StackSamplerLoopManager.h"
#include "IClrLifetime.h"
#include "OpSysTools.h"
#include "OsSpecificApi.h"
#include "SymbolsResolver.h"
#include "ThreadsCpuManager.h"

using namespace std::chrono_literals;

constexpr std::chrono::milliseconds DeadlockDetectionInterval = 1s;
constexpr std::chrono::milliseconds StackSamplerLoopManager_MaxExpectedStackSampleCollectionDurationMs = 500ms;
constexpr std::chrono::nanoseconds CollectionDurationThresholdNs = std::chrono::nanoseconds(StackSamplerLoopManager_MaxExpectedStackSampleCollectionDurationMs);

#ifdef NDEBUG
constexpr std::chrono::milliseconds StatsAggregationPeriodMs = 30000ms;
#else
constexpr std::chrono::milliseconds StatsAggregationPeriodMs = 10000ms;
#endif
constexpr std::chrono::nanoseconds StatsAggregationPeriodNs = StatsAggregationPeriodMs;

const WCHAR* WatcherThreadName = WStr("DD.Profiler.StackSamplerLoopManager.WatcherThread");

StackSamplerLoopManager* StackSamplerLoopManager::s_singletonInstance = nullptr;
const std::chrono::nanoseconds StackSamplerLoopManager::StatisticAggregationPeriodNs = 10s;


void StackSamplerLoopManager::CreateNewSingletonInstance(ICorProfilerInfo4* pCorProfilerInfo, std::shared_ptr<IMetricsSender> metricsSender, IClrLifetime const* clrLifetime)
{
    StackSamplerLoopManager* newSingletonInstance = new StackSamplerLoopManager(pCorProfilerInfo, metricsSender, clrLifetime);

    StackSamplerLoopManager::DeleteSingletonInstance();
    StackSamplerLoopManager::s_singletonInstance = newSingletonInstance;
}

StackSamplerLoopManager* StackSamplerLoopManager::GetSingletonInstance()
{
    StackSamplerLoopManager* singletonInstance = StackSamplerLoopManager::s_singletonInstance;
    if (singletonInstance != nullptr)
    {
        return singletonInstance;
    }

    throw std::logic_error("No singleton instance of StackSamplerLoopManager has been created yet, or it has already been deleted.");
}

void StackSamplerLoopManager::DeleteSingletonInstance(void)
{
    StackSamplerLoopManager* singletonInstance = StackSamplerLoopManager::s_singletonInstance;
    if (singletonInstance != nullptr)
    {
        StackSamplerLoopManager::s_singletonInstance = nullptr;
        delete singletonInstance;
    }
}

StackSamplerLoopManager::StackSamplerLoopManager(ICorProfilerInfo4* pCorProfilerInfo, std::shared_ptr<IMetricsSender> metricsSender, IClrLifetime const* clrLifetime) :
    _pCorProfilerInfo{pCorProfilerInfo},
    _pStackFramesCollector{nullptr},
    _pStackSamplerLoop{nullptr},
    _pWatcherThread{nullptr},
    _isWatcherShutdownRequested{false},
    _pTargetThread{nullptr},
    _collectionStartNs{0},
    _isTargetThreadSuspended{false},
    _isForceTerminated{false},
    _currentPeriod{0},
    _currentPeriodStartNs{0},
    _deadlocksInPeriod{0},
    _totalDeadlockDetectionsCount{0},
    _metricsSender{metricsSender},
    _statisticsReadyToSend{nullptr},
    _pClrLifetime{clrLifetime},
    _deadlockInterventionInProgress{0}
{
    _pCorProfilerInfo->AddRef();
    _pStackFramesCollector = OsSpecificApi::CreateNewStackFramesCollectorInstance(_pCorProfilerInfo);

    _currentStatistics = std::make_unique<Statistics>();
    _statisticCollectionStartNs = OpSysTools::GetHighPrecisionNanoseconds();

    this->RunStackSampling();

    if (AllowDeadlockIntervention)
    {
        this->RunWatcher();
    }
}

StackSamplerLoopManager::~StackSamplerLoopManager()
{
    GracefulShutdownStackSampling();

    if (AllowDeadlockIntervention)
    {
        ShutdownWatcher();
    }

    StackFramesCollectorBase* pStackFramesCollector = _pStackFramesCollector;
    if (pStackFramesCollector != nullptr)
    {
        delete pStackFramesCollector;
        _pStackFramesCollector = nullptr;
    }

    ICorProfilerInfo4* pCorProfilerInfo = _pCorProfilerInfo;
    if (pCorProfilerInfo != nullptr)
    {
        pCorProfilerInfo->Release();
        _pCorProfilerInfo = nullptr;
    }
}

void StackSamplerLoopManager::RunStackSampling(void)
{
    // This API is not intended to be thread-safe when called concurrently!

    StackSamplerLoop* stackSamplerLoop = _pStackSamplerLoop;
    if (stackSamplerLoop == nullptr)
    {
        assert(_pStackFramesCollector != nullptr);

        stackSamplerLoop = new StackSamplerLoop(_pCorProfilerInfo, _pStackFramesCollector, this);
        _pStackSamplerLoop = stackSamplerLoop;
    }
}

void StackSamplerLoopManager::GracefulShutdownStackSampling(void)
{
    StackSamplerLoop* stackSamplerLoop = _pStackSamplerLoop;
    if (stackSamplerLoop != nullptr)
    {
        stackSamplerLoop->RequestShutdown();
        stackSamplerLoop->Join();
        delete stackSamplerLoop;
        _pStackSamplerLoop = nullptr;
    }
}

void StackSamplerLoopManager::RunWatcher(void)
{
    _pWatcherThread = new std::thread(&StackSamplerLoopManager::WatcherLoop, this);
    OpSysTools::SetNativeThreadName(_pWatcherThread, WatcherThreadName);
}

void StackSamplerLoopManager::ShutdownWatcher(void)
{
    std::thread* pWatcherThread = _pWatcherThread;
    if (pWatcherThread != nullptr)
    {
        _isWatcherShutdownRequested = true;

        pWatcherThread->join();

        delete pWatcherThread;
        _pWatcherThread = nullptr;
    }
}

void StackSamplerLoopManager::WatcherLoop(void)
{
    Log::Info("StackSamplerLoopManager::WatcherLoop started.");
    ThreadsCpuManager::GetSingletonInstance()->Map(OpSysTools::GetThreadId(), WatcherThreadName);

    while (false == _isWatcherShutdownRequested)
    {
        try
        {
            std::this_thread::sleep_for(DeadlockDetectionInterval);

            WatcherLoopIteration();
            SendStatistics();
        }
        catch (const std::runtime_error& re)
        {
            Log::Error("Runtime error in StackSamplerLoopManager::WatcherLoop: ", re.what());
        }
        catch (const std::exception& ex)
        {
            Log::Error("Typed Exception in StackSamplerLoopManager::WatcherLoop: ", ex.what());
        }
        catch (...)
        {
            Log::Error("Unknown Exception in StackSamplerLoopManager::WatcherLoop.");
        }
    }

    Log::Info("StackSamplerLoopManager::WatcherLoop finished.");
}

void StackSamplerLoopManager::SendStatistics()
{
    // could be null on customer site (metrics are only sent in Reliability Environment)
    if (_metricsSender == nullptr || _statisticsReadyToSend == nullptr)
        return;

    _metricsSender->Gauge(Statistics::MeanSuspensionTimeMetricName, _statisticsReadyToSend->GetMeanSuspensionTime());
    _metricsSender->Gauge(Statistics::MaxSuspensionTimeMetricName, _statisticsReadyToSend->GetMaxSuspensionTime());
    _metricsSender->Gauge(Statistics::MeanCollectionTimeMetricName, _statisticsReadyToSend->GetMeanCollectionTime());
    _metricsSender->Gauge(Statistics::MaxCollectionTimeMetricName, _statisticsReadyToSend->GetMaxCollectionTime());
    _metricsSender->Counter(Statistics::TotalDeadlocksMetricName, _statisticsReadyToSend->GetTotalDeadlocks());

    Log::Debug("Sampling metrics have been sent. ");

    _statisticsReadyToSend.reset();
}

void StackSamplerLoopManager::WatcherLoopIteration(void)
{
    std::lock_guard<std::mutex> guardedLock(_watcherActivityLock);

    // Check whether the statistics aggregation period has completed.
    // If yes, reset the counters and log the stats, if the correspionding conditions are met.
    std::int64_t currentNanosecs = OpSysTools::GetHighPrecisionNanoseconds();
    std::chrono::nanoseconds periodDurationNs =
        (_currentPeriodStartNs == 0)
            ? 0ns
            : std::chrono::nanoseconds(currentNanosecs - _currentPeriodStartNs);

    if (_currentPeriodStartNs == 0 || periodDurationNs > StatsAggregationPeriodNs)
    {
        StartNewStatsAggregationPeriod(currentNanosecs, periodDurationNs);
    }

    // Check whether a stack sample collection is ongoing
    if (_collectionStartNs == 0)
    {
        return;
    }

    if (_deadlockInterventionInProgress >= 1)
    {
        _deadlockInterventionInProgress++;
        Log::Error("StackSamplerLoopManager::WatcherLoopIteration - Deadlock intervention still in progress for thread ", _pTargetThread->GetOsThreadId(),
                   std::hex, " (= 0x", _pTargetThread->GetOsThreadId(), ")");
        // TODO: Validate that calling resuming again (and again) could unlock the situation.
        // The previous call to ResumeThread failed.
        return;
    }

    // ! ! ! ! ! ! ! ! ! ! !
    // At this point we know that a collection is ongoing.
    // This means that the target thread MAY be either already suspended or may be suspended any time:
    //
    // The StackSamplerLoop has already called AllowStackWalk(..)
    // (because pCurrentStackSampleCollectionThreadInfo is not nullptr).
    // But at this point we have not yet checked if (false == _isTargetThreadSuspended),
    // so we do not know whether StackSamplerLoop is about to suspend the target thread, has already suspended it,
    // or already called SuspendTargetThreadIfRequired(..) and decided not to suspend it. Whatever the case, it may
    // happen concurrently because any of those actions does not require to be done under the _watcherActivityLock.
    //
    // So we must behave according to the thread-suspended mode:
    //    No logging, no allocations, no APIs that might allocate, log or take any shared or global locks.
    //
    // The LogDuringStackSampling_Unsafe switch allows to enable some UNSAFE logging.
    // ONLY USE IT TO DEBUG ISSUES WITH POSSIBLE DEADLOCK.
    // ! ! ! ! ! ! ! ! ! ! !

    std::chrono::nanoseconds collectionDurationNs = std::chrono::nanoseconds(currentNanosecs - _collectionStartNs);
    if (collectionDurationNs <= CollectionDurationThresholdNs)
    {
        // under the termination threshold
        return;
    }

#ifdef _WINDOWS
    auto samplerThreadhandle = static_cast<HANDLE>(_pStackSamplerLoop->_pLoopThread->native_handle());

    FILETIME creationTime, exitTime, kernelTime, userTime;
    GetThreadTimes(samplerThreadhandle, &creationTime, &exitTime, &kernelTime, &userTime);

    if (HasMadeProgress(userTime, kernelTime))
    {
        _userTime = userTime;
        _kernelTime = kernelTime;
        return;
    }
#endif

    _currentStatistics->IncrDeadlockCount();

    PerformDeadlockIntervention(collectionDurationNs);
}

bool StackSamplerLoopManager::HasMadeProgress(FILETIME userTime, FILETIME kernelTime)
{
    return userTime.dwLowDateTime != _userTime.dwLowDateTime ||
           userTime.dwHighDateTime != _userTime.dwHighDateTime ||
           kernelTime.dwLowDateTime != _kernelTime.dwLowDateTime ||
           kernelTime.dwHighDateTime != _kernelTime.dwHighDateTime;
}

void StackSamplerLoopManager::PerformDeadlockIntervention(const std::chrono::nanoseconds& ongoingStackSampleCollectionDurationNs)
{
    _deadlockInterventionInProgress = 1;

    // ! ! ! ! ! ! ! ! ! ! !
    // This private method is invoked by WatcherLoopIteration().
    // It MUST be invoked ONLY while holding the _watcherActivityLock.
    //
    // When this method is invoked, we know that the profiling target thread is suspended.
    // Therefore, all restrictions applicable to the thread-suspended mode apply:
    // No logging, no allocations, no APIs that might allocate, log or take any shared or global locks.
    // ! ! ! ! ! ! ! ! ! ! !

    // ** Collect statistics about this deadlock:

    // Determine if the thread was fit for collection previously
    // (this will also reset it's internal state for the current period if applicable)
    bool wasThreadSafeForStackSampleCollection = GetUpdateIsThreadSafeForStackSampleCollection(_pTargetThread, nullptr);

    _pTargetThread->IncDeadlocksCount();
    _deadlocksInPeriod++;
    _totalDeadlockDetectionsCount++;

    // Determine if the thread is fit for future collections. If this status changed, we will log it when safe.
    bool isThreadSafeForStackSampleCollection = GetUpdateIsThreadSafeForStackSampleCollection(_pTargetThread, nullptr);

    _pStackFramesCollector->RequestAbortCurrentCollection();

    // resume target thread
    uint32_t hr;
    _pStackFramesCollector->ResumeTargetThreadIfRequired(_pTargetThread,
                                                         _isTargetThreadSuspended,
                                                         &hr);

    // don't forget to resume the target thread if needed (required in 32 bit)
    _pStackFramesCollector->OnDeadlock();

    LogDeadlockIntervention(ongoingStackSampleCollectionDurationNs,
                            wasThreadSafeForStackSampleCollection,
                            isThreadSafeForStackSampleCollection,
                            SUCCEEDED(hr));

    // The sampled thread has been resumed. The lock blocking the stack sampler loop should be released anytime soon.
}

void StackSamplerLoopManager::LogDeadlockIntervention(const std::chrono::nanoseconds& ongoingStackSampleCollectionDurationNs,
                                                      bool wasThreadSafeForStackSampleCollection,
                                                      bool isThreadSafeForStackSampleCollection,
                                                      bool isThreadResumed)
{
    // ** Log a notice of this intervention:
    // (only if we successfully resumed the target thread, as it is not safe otherwise)

    std::uint64_t threadDeadlockTotalCount;
    std::uint64_t threadDeadlockInAggPeriodCount;
    std::uint64_t threadUsedDeadlocksAggPeriodIndex;
    _pTargetThread->GetDeadlocksCount(&threadDeadlockTotalCount,
                                      &threadDeadlockInAggPeriodCount,
                                      &threadUsedDeadlocksAggPeriodIndex);

    Log::Info("StackSamplerLoopManager::PerformDeadlockIntervention(): The ongoing StackSampleCollection duration crossed the threshold."
              " A deadlock intervention was performed."
              " Deadlocked target thread=(OsThreadId=",
              std::dec, _pTargetThread->GetOsThreadId(), ", ",
              " ClrThreadId=0x", std::hex, _pTargetThread->GetClrThreadId(), ");", std::dec,
              " ongoingStackSampleCollectionDurationNs=", ToMillis(ongoingStackSampleCollectionDurationNs), " millisecs;",
              " _isTargetThreadResumed=", std::boolalpha, isThreadResumed, ";",
              " _currentPeriod=", _currentPeriod, ";",
              " _deadlocksInPeriod=", _deadlocksInPeriod, ";",
              " _totalDeadlockDetectionsCount=", _totalDeadlockDetectionsCount, ";",
              " wasThreadSafeForStackSampleCollection=", std::boolalpha, wasThreadSafeForStackSampleCollection, ";",
              " isThreadSafeForStackSampleCollection=", isThreadSafeForStackSampleCollection, ";", std::noboolalpha,
              " threadDeadlockTotalCount=", threadDeadlockTotalCount, ";",
              " threadDeadlockInAggPeriodCount=", threadDeadlockInAggPeriodCount, ";",
              " threadUsedDeadlocksAggPeriodIndex=", threadUsedDeadlocksAggPeriodIndex, ".");

    if (wasThreadSafeForStackSampleCollection != isThreadSafeForStackSampleCollection)
    {
        Log::Info("ShouldCollectThread status changed in PerformDeadlockIntervention"
                  " for thread (OsThreadId=",
                  _pTargetThread->GetOsThreadId(),
                  ", ClrThreadId=0x", std::hex, _pTargetThread->GetClrThreadId(), std::dec,
                  ", ThreadName=\"", _pTargetThread->GetThreadName(),
                  " wasThreadSafeForStackSampleCollection=", std::boolalpha, wasThreadSafeForStackSampleCollection, ";",
                  " isThreadSafeForStackSampleCollection=", isThreadSafeForStackSampleCollection, ";", std::noboolalpha,
                  " threadDeadlockTotalCount=", threadDeadlockTotalCount, ";",
                  " threadDeadlockInAggPeriodCount=", threadDeadlockInAggPeriodCount, ";",
                  " threadUsedDeadlocksAggPeriodIndex=", threadUsedDeadlocksAggPeriodIndex, ";",
                  " _deadlocksInPeriod=", _deadlocksInPeriod, ".");
    }
}

void StackSamplerLoopManager::StartNewStatsAggregationPeriod(std::int64_t currentHighPrecisionNanosecs,
                                                             const std::chrono::nanoseconds& periodDurationNs)
{
    // This method must only be called while _watcherActivityLock is held!

    // We log the stats only if there is no ongoing stack sampling going on
    // (as we are then guaranteed not to get into suspended thread caused deadlock)
    // or if unsafe logging during stack walks was explicitly opted into.
    if (LogDuringStackSampling_Unsafe || _pTargetThread == nullptr)
    {
        // Do not flood logs:
        // If we detected issues in this period, always log an info message,
        // if we did not, then only log a debug message.
        if (_deadlocksInPeriod > 0)
        {
            Log::Info("StackSamplerLoopManager: Completing a StatsAggregationPeriod.",
                      " Period-Index=", _currentPeriod, ",",
                      " Targeted-PediodDuration=", StatsAggregationPeriodMs.count(), " millisec,",
                      " Actual-PediodDuration=", ToMillis(periodDurationNs), " millisec,",
                      " Period-DeadlockDetectionsCount=", _deadlocksInPeriod, ",",
                      " AppLifetime-DeadlockDetectionsCount=", _totalDeadlockDetectionsCount, ".");
        }
        else if (Log::IsDebugEnabled())
        {
            Log::Debug("StackSamplerLoopManager: Completing a StatsAggregationPeriod.",
                       " Period-Index=", _currentPeriod, ",",
                       " Targeted-PediodDuration=", StatsAggregationPeriodMs.count(), " millisec,",
                       " Actual-PediodDuration=", ToMillis(periodDurationNs), " millisec,",
                       " Period-DeadlockDetectionsCount=", _deadlocksInPeriod, ",",
                       " AppLifetime-DeadlockDetectionsCount=", _totalDeadlockDetectionsCount, ".");
        }
    }

    _currentPeriod++;
    _currentPeriodStartNs = currentHighPrecisionNanosecs;
    _deadlocksInPeriod = 0;
}

double StackSamplerLoopManager::ToMillis(const std::chrono::nanoseconds& nanosecs)
{
    return nanosecs.count() / 1000000.0;
}

bool StackSamplerLoopManager::AllowStackWalk(ManagedThreadInfo* pThreadInfo)
{
    std::lock_guard<std::mutex> guardedLock(_watcherActivityLock);

    bool isThreadSafeStatusChanged;
    bool isThreadSafeForStackSampleCollection = GetUpdateIsThreadSafeForStackSampleCollection(pThreadInfo, &isThreadSafeStatusChanged);

    if (isThreadSafeStatusChanged)
    {
        std::uint64_t threadDeadlockTotalCount;
        std::uint64_t threadDeadlockInAggPeriodCount;
        std::uint64_t threadUsedDeadlocksAggPeriodIndex;
        pThreadInfo->GetDeadlocksCount(&threadDeadlockTotalCount,
                                       &threadDeadlockInAggPeriodCount,
                                       &threadUsedDeadlocksAggPeriodIndex);

        // At that step, the target thread is not suspended so no deadlock risk when logging
        Log::Info("ShouldCollectThread status changed in AllowStackWalk",
                  " for thread (OsThreadId=", pThreadInfo->GetOsThreadId(),
                  ", ClrThreadId=0x", std::hex, pThreadInfo->GetClrThreadId(), std::dec,
                  ", ThreadName=\"", pThreadInfo->GetThreadName(), "\"):",
                  " ShouldCollectThread=", isThreadSafeForStackSampleCollection, ";",
                  " threadDeadlockTotalCount=", threadDeadlockTotalCount, ";",
                  " threadDeadlockInAggPeriodCount=", threadDeadlockInAggPeriodCount, ";",
                  " threadUsedDeadlocksAggPeriodIndex=", threadUsedDeadlocksAggPeriodIndex, ";",
                  " _deadlocksInPeriod=", _deadlocksInPeriod, ".");
    }


    if (!isThreadSafeForStackSampleCollection)
    {
        return false;
    }

    // According to
    // https://sourcegraph.com/github.com/dotnet/runtime/-/blob/src/coreclr/vm/proftoeeinterfaceimpl.cpp?L8479
    // we _must_ block in ICorProfilerCallback::ThreadDestroyed to prevent the thread from being destroyed
    // while walking its callstack.
    pThreadInfo->GetStackWalkLock().Acquire();

    // We _must_ check if the thread was not destroyed while acquiring the lock
    if (pThreadInfo->IsDestroyed())
    {
        pThreadInfo->GetStackWalkLock().Release();
        return false;
    }

    pThreadInfo->AddRef();
    _pTargetThread = pThreadInfo;
    _isTargetThreadSuspended = false;
    _isForceTerminated = false;

    return true;
}

void StackSamplerLoopManager::NotifyThreadState(bool isSuspended)
{
    std::lock_guard<std::mutex> guardedLock(_watcherActivityLock);

    _isTargetThreadSuspended = isSuspended;
    _threadSuspensionStart = OpSysTools::GetHighPrecisionNanoseconds();
}

void StackSamplerLoopManager::NotifyCollectionStart()
{
    std::lock_guard<std::mutex> guardedLock(_watcherActivityLock);

    _collectionStartNs = OpSysTools::GetHighPrecisionNanoseconds();

#ifdef _WINDOWS
    auto samplerThreadhandle = static_cast<HANDLE>(_pStackSamplerLoop->_pLoopThread->native_handle());
    FILETIME creationTime, exitTime;
    GetThreadTimes(samplerThreadhandle, &creationTime, &exitTime, &_kernelTime, &_userTime);
#endif
}

void StackSamplerLoopManager::NotifyCollectionEnd()
{
    std::lock_guard<std::mutex> guardedLock(_watcherActivityLock);

    std::int64_t collectionEndTimeNs = OpSysTools::GetHighPrecisionNanoseconds();
    _currentStatistics->AddCollectionTime(collectionEndTimeNs - _collectionStartNs);

    _collectionStartNs = 0;
    _kernelTime = {0};
    _userTime = {0};
    _deadlockInterventionInProgress = 0;
}

void StackSamplerLoopManager::NotifyIterationFinished()
{
    std::lock_guard<std::mutex> guardedLock(_watcherActivityLock);

    _pTargetThread->GetStackWalkLock().Release();
    _pTargetThread->Release();
    _pTargetThread = nullptr;
    _collectionStartNs = 0;
    _isTargetThreadSuspended = false;

    std::int64_t threadCollectionEndTimeNs = OpSysTools::GetHighPrecisionNanoseconds();
    _currentStatistics->AddSuspensionTime(threadCollectionEndTimeNs - _threadSuspensionStart);

    if (threadCollectionEndTimeNs - _statisticCollectionStartNs >= StatisticAggregationPeriodNs.count())
    {
        Log::Debug("Notify-ThreadStackSampleCollection-Finished invoked - Prepare statistics to be sent.");
        _statisticsReadyToSend.reset(_currentStatistics.release());
        _currentStatistics = std::make_unique<Statistics>();
        _statisticCollectionStartNs = threadCollectionEndTimeNs;
    }
}

inline bool StackSamplerLoopManager::GetUpdateIsThreadSafeForStackSampleCollection(
    ManagedThreadInfo* pThreadInfo,
    bool* pIsStatusChanged)
{
    // This method must only be called while _watcherActivityLock is held!

    std::uint64_t prevAggPeriodDeadlockCount;
    std::uint64_t currAggPeriodDeadlockCount;
    pThreadInfo->GetOrResetDeadlocksCount(
        _currentPeriod,
        &prevAggPeriodDeadlockCount,
        &currAggPeriodDeadlockCount);

    bool wasThreadSafeForStackSampleCollection =
        ShouldCollectThread(
            prevAggPeriodDeadlockCount,
            _deadlocksInPeriod);
    bool isThreadSafeForStackSampleCollection =
        ShouldCollectThread(
            currAggPeriodDeadlockCount,
            _deadlocksInPeriod);

    if (pIsStatusChanged != nullptr)
    {
        *pIsStatusChanged = (wasThreadSafeForStackSampleCollection != isThreadSafeForStackSampleCollection);
    }

    return isThreadSafeForStackSampleCollection;
}

inline bool StackSamplerLoopManager::ShouldCollectThread(
    std::uint64_t threadAggPeriodDeadlockCount,
    std::uint64_t globalAggPeriodDeadlockCount) const
{
    return (threadAggPeriodDeadlockCount <= DeadlocksPerThreadThreshold) &&
           (globalAggPeriodDeadlockCount <= TotalDeadlocksThreshold);
}