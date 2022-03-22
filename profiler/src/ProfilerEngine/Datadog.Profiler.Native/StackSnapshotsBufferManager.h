// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <vector>

#include "ManagedThreadInfo.h"
#include "StackSnapshotResultReusableBuffer.h"
#include "StackSnapshotsBufferSegment.h"
#include "SynchronousOffThreadWorkerBase.h"
#include "IStackSnapshotsBufferManager.h"

// forward declarations
class IThreadsCpuManager;
class ISymbolsResolver;


class StackSnapshotsBufferManager : public IStackSnapshotsBufferManager
{
private:
    static const std::uint32_t BufferSegmentSizeBytes; // 1 MByte
    static const std::uint32_t BufferSegmentsCountMax; // 20

public:
    StackSnapshotsBufferManager(IThreadsCpuManager* pThreadsCpuManager, ISymbolsResolver* pSymbolsResolver);

// interfaces implementation
public:
    const char* GetName() override;
    bool Start() override;
    bool Stop() override;
    void Add(const StackSnapshotResultBuffer* pSnapshotResult, const ManagedThreadInfo* pThreadInfo) override;
    // methods called from managed code via P/Invoke
    bool TryCompleteCurrentWriteSegment() override;
    bool TryMakeSegmentAvailableForWrite(StackSnapshotsBufferSegment* segment) override;

private:
    ~StackSnapshotsBufferManager() override;
    StackSnapshotsBufferManager(StackSnapshotsBufferManager const&) = delete;
    StackSnapshotsBufferManager& operator=(StackSnapshotsBufferManager const&) = delete;

private:
    StackSnapshotsBufferSegment* GetSegment();
    bool NotifyForExport_Offload(StackSnapshotsBufferSegment* segment);

    class CompleteAndEnqueueSegmentForExport_OffThreadWorker : public SynchronousOffThreadWorkerBase
    {
    public:
        explicit CompleteAndEnqueueSegmentForExport_OffThreadWorker(StackSnapshotsBufferManager* pOwner, IThreadsCpuManager* pThreadsCpuManager);
        CompleteAndEnqueueSegmentForExport_OffThreadWorker() = delete;
        ~CompleteAndEnqueueSegmentForExport_OffThreadWorker() override;

    protected:
        virtual bool ShouldInitializeCurrentThreadforManagedInteractions(ICorProfilerInfo4** ppCorProfilerInfo) override;
        virtual bool ShouldSetManagedThreadName(const char** managedThreadName) override;
        virtual bool ShouldSetNativeThreadName(const WCHAR** nativeThreadName) override;
        virtual void PerformWork(void* pParameters, void* pResults) override;

    private:
        static const char* ManagedThreadName;
        static const WCHAR* NativeThreadName;
        StackSnapshotsBufferManager* _pOwner;
    };

private:
    const char* _serviceName = "StackSnapshotsBufferManager";

    ISymbolsResolver* _pSymbolsResolver = nullptr;
    std::recursive_mutex _modificationLock;

    // Snapshots are added into a segment.
    // When the segment is full, it is "sent" to managed code:
    // --> a new one is either created or retrieved from the free list
    // Once a segment is processed by the managed side, it is added back to the free list
    // If the segment could not be sent to managed side (i.e. managed side not already started),
    // it is kept in a pending list.
    std::vector<StackSnapshotsBufferSegment*> _freeSegments;
    std::vector<StackSnapshotsBufferSegment*> _pendingSegments;
    StackSnapshotsBufferSegment* _current;
    std::uint32_t _segmentsInManagedCount;

private:
    bool TooManySegmentsInMemory();
    void SendPendingSegments();
    void ToString(time_t& unixTime, char* buffer, size_t bufferLen) const;

    CompleteAndEnqueueSegmentForExport_OffThreadWorker _completeAndEnqueueSegment_Worker;
};

