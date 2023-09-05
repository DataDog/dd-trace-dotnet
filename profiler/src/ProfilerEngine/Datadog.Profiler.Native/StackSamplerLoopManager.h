// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <chrono>
#include <memory>
#include <thread>
#include <unordered_map>

// from dotnet coreclr includes
#include "cor.h"
#include "corprof.h"
// end

#include "CounterMetric.h"
#include "ICollector.h"
#include "IMetricsSender.h"
#include "IStackSamplerLoopManager.h"
#include "Log.h"
#include "MeanMaxMetric.h"
#include "MetricsRegistry.h"
#include "OpSysTools.h"
#include "RawCpuSample.h"
#include "RawWallTimeSample.h"
#include "StackSamplerLoop.h"
#include "MetricsRegistry.h"
#include "CounterMetric.h"


// forward declaration
class IClrLifetime;
class IThreadsCpuManager;
class IStackSnapshotsBufferManager;
class IManagedThreadList;
class ISymbolsResolver;
class IConfiguration;

constexpr std::uint64_t DeadlocksPerThreadThreshold = 5;
constexpr std::uint64_t TotalDeadlocksThreshold = 12;

// Be very careful with this flag. See comments for the StackSamplerLoopManager class and throughout the implementation.
constexpr bool LogDuringStackSampling_Unsafe = false;

/// <summary>
/// The process-singleton instance of this class owns and manages the StackSamplerLoop of the process.
///
/// The StackSamplerLoopManager monitors the duration of stacks sample collections initiated by the StackSamplerLoop.
/// If a collection takes too long, it is assumed to be stale due to a deadlock, and an intervention is performed.
/// For this, the StackSamplerLoopManager notifies the StackSamplerLoop's thread and resumes the target thread.
///
/// In addition, StackSamplerLoopManager also reasons about the safety of collecting stack samples from any particular thread based on
/// how many interventions were performed in the past. If too many interventions were performed in a recent period, stack collections
/// are suspended for a period of time.
///
/// Details:
/// * StackSamplerLoop calls NotifyXxx(..) methods on StackSamplerLoopManager to tell about stack sample collections.
///   StackSamplerLoop reacts to the NotifyXxx(..) invocations by keeping track of the duration of the current collection, of which
///   thread is targeted for collection, and of other relevant details.
///   It also uses the NotifyXxx(..) calls to communicate whether a thread is deemed safe for sample collection in the manner described above.
/// * Safety-statistics are aggregated per period (see StatsAggregationPeriodMs in the .cpp file).
///   After the period restarts, all threads are considered safe again and the counters recycle.
/// * If a thread deadlocks more then DeadlocksPerThreadThreshold times per period,
///   is is considered unsafe for the rest of the period.
/// * If the total number of deadlocks is more than TotalDeadlocksThreshold,
///   then no thread is considered safe for the rest of the period.
/// * Logging can allocate, and allocation can take global locks to manage the heap. Thus, we should nerver log while a thread is suspended.
///   However, logging may be required for investigating some of the issues related to the areas of concern of this class.
///   The LogDuringStackSampling_Unsafe allows turning on such unsafe logging during investigations.
///   Don't be surprised if the application deadlock while using that flag!
/// </summary>
class StackSamplerLoopManager : public IStackSamplerLoopManager
{
public:
    StackSamplerLoopManager(
        ICorProfilerInfo4* pCorProfilerInfo,
        IConfiguration* pConfiguration,
        std::shared_ptr<IMetricsSender> metricsSender,
        IClrLifetime const* clrLifetime,
        IThreadsCpuManager* pThreadsCpuManager,
        IManagedThreadList* pManagedThreadList,
        IManagedThreadList* pCodeHotspotThreadList,
        ICollector<RawWallTimeSample>* pWallTimeCollector,
        ICollector<RawCpuSample>* pCpuTimeCollector,
        MetricsRegistry& metricsRegistry);

    ~StackSamplerLoopManager() override;

public:
    const char* GetName() override;
    bool Start() override;
    bool Stop() override;
    bool AllowStackWalk(std::shared_ptr<ManagedThreadInfo> pThreadInfo) override;
    void NotifyThreadState(bool isSuspended) override;
    void NotifyCollectionStart() override;
    void NotifyCollectionEnd() override;
    void NotifyIterationFinished() override;

private:
    StackSamplerLoopManager() = delete;

    inline bool GetUpdateIsThreadSafeForStackSampleCollection(ManagedThreadInfo* pThreadInfo, bool* pIsStatusChanged);
    static inline bool ShouldCollectThread(std::uint64_t threadAggPeriodDeadlockCount, std::uint64_t globalAggPeriodDeadlockCount) ;

    void RunStackSampling();
    void GracefulShutdownStackSampling();

    void RunWatcher();
    void ShutdownWatcher();

    void WatcherLoop();
    void WatcherLoopIteration();
    void PerformDeadlockIntervention(const std::chrono::nanoseconds& ongoingStackSampleCollectionDurationNs);
    void LogDeadlockIntervention(
        const std::chrono::nanoseconds& ongoingStackSampleCollectionDurationNs,
        bool wasThreadSafeForStackSampleCollection,
        bool isThreadSafeForStackSampleCollection,
        bool isThreadResumed);

    void StartNewStatsAggregationPeriod(std::int64_t currentHighPrecisionNanosecs,
                                        const std::chrono::nanoseconds& periodDurationNs);

    static double ToMillis(const std::chrono::nanoseconds& nanosecs);

private:
    static const std::chrono::nanoseconds StatisticAggregationPeriodNs;

    class Statistics
    {
    public:
        static inline const std::string MeanSuspensionTimeMetricName = "datadog.profiling.dotnet.operational.suspensions.time.mean";
        static inline const std::string MaxSuspensionTimeMetricName = "datadog.profiling.dotnet.operational.suspensions.time.max";

        static inline const std::string MeanCollectionTimeMetricName = "datadog.profiling.dotnet.operational.collections.time.mean";
        static inline const std::string MaxCollectionTimeMetricName = "datadog.profiling.dotnet.operational.collections.time.max";

        static inline const std::string TotalDeadlocksMetricName = "datadog.profiling.dotnet.operational.deadlocks";

        Statistics() = default;

        void AddSuspensionTime(std::uint64_t suspensionTime)
        {
            _totalSuspensionTime += suspensionTime;
            _maxSuspensionTime = (std::max)(suspensionTime, _maxSuspensionTime);
            _totalSuspensions++;
        }
        double GetMaxSuspensionTime() const
        {
            return (double)_maxSuspensionTime;
        }
        double GetMeanSuspensionTime() const
        {
            return (double)_totalSuspensionTime / _totalSuspensions;
        }

        void AddCollectionTime(std::uint64_t collectionTime)
        {
            _totalCollectionTime += collectionTime;
            _maxCollectionTime = (std::max)(collectionTime, _maxCollectionTime);
            _totalCollections++;
        }
        double GetMaxCollectionTime() const
        {
            return (double)_maxCollectionTime;
        }
        double GetMeanCollectionTime() const
        {
            return (double)_totalCollectionTime / _totalCollections;
        }

        void IncrDeadlockCount()
        {
            _totalDeadlocks++;
        }

        std::uint64_t GetTotalDeadlocks() const
        {
            return _totalDeadlocks;
        }

    private:
        std::uint64_t _totalSuspensionTime;
        std::uint64_t _maxSuspensionTime;
        std::uint64_t _totalSuspensions;

        std::uint64_t _totalCollectionTime;
        std::uint64_t _maxCollectionTime;
        std::uint64_t _totalCollections;

        std::uint64_t _totalDeadlocks;
    };

private:
    void SendStatistics();
    bool HasMadeProgress(FILETIME userTime, FILETIME kernelTime);

private:
    const char* _serviceName = "StackSamplerLoopManager";
    ICorProfilerInfo4* _pCorProfilerInfo;
    IConfiguration* _pConfiguration = nullptr;
    IThreadsCpuManager* _pThreadsCpuManager = nullptr;
    IManagedThreadList* _pManagedThreadList = nullptr;
    IManagedThreadList* _pCodeHotspotsThreadList = nullptr;
    ICollector<RawWallTimeSample>* _pWallTimeCollector = nullptr;
    ICollector<RawCpuSample>* _pCpuTimeCollector = nullptr;

    std::unique_ptr<StackFramesCollectorBase> _pStackFramesCollector;
    std::unique_ptr<StackSamplerLoop> _pStackSamplerLoop;
    std::uint8_t _deadlockInterventionInProgress;

    std::unique_ptr<std::thread> _pWatcherThread;
    bool _isWatcherShutdownRequested;

    std::mutex _watcherActivityLock;

    std::shared_ptr<ManagedThreadInfo> _pTargetThread;
    std::int64_t _collectionStartNs;
    FILETIME _kernelTime, _userTime;

    bool _isTargetThreadSuspended;
    bool _isForceTerminated;

    std::uint64_t _currentPeriod;
    std::int64_t _currentPeriodStartNs;
    std::uint64_t _deadlocksInPeriod;
    std::uint64_t _totalDeadlockDetectionsCount;

    // The stack sampler loop is filling up the Current statistics and
    // when 10s elapsed, move them to the Ready statistics.
    // The stack sampler manager will only look at the Ready statistics if not null.
    // It means that there is no need to thread-synchronize the access to these fields
    std::shared_ptr<IMetricsSender> _metricsSender;
    std::int64_t _statisticCollectionStartNs;
    std::int64_t _threadSuspensionStart;
    std::unique_ptr<Statistics> _statisticsReadyToSend;
    std::unique_ptr<Statistics> _currentStatistics;
    MetricsRegistry& _metricsRegistry;
    std::shared_ptr<CounterMetric> _deadlockCountMetric;

    IClrLifetime const* _pClrLifetime;
    bool _isStopped = false;
};
