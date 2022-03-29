// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include <stdexcept>

#define __STDC_WANT_LIB_EXT1__ 1
#include<time.h>

#include "Log.h"
#include "ScopeFinalizer.h"
#include "StackSnapshotResult.h"
#include "StackSnapshotsBufferManager.h"
#include "IThreadsCpuManager.h"


const std::uint32_t StackSnapshotsBufferManager::BufferSegmentSizeBytes = 0x100000; // 1 MB

// This is the threshold used to limit allocated segments:
//   Count the segments sent to managed - segments in the free list >= threshold
//   When this happens, stop accepting new snapshots
const std::uint32_t StackSnapshotsBufferManager::BufferSegmentsCountMax = 20;  // 20 MB


StackSnapshotsBufferManager::StackSnapshotsBufferManager(IThreadsCpuManager* pThreadsCpuManager, ISymbolsResolver* pSymbolsResolver) :
    _completeAndEnqueueSegment_Worker{this, pThreadsCpuManager},
    _pSymbolsResolver{pSymbolsResolver}
{
    _current = new StackSnapshotsBufferSegment(StackSnapshotsBufferManager::BufferSegmentSizeBytes, pSymbolsResolver);
    _current->AddRef();
    _segmentsInManagedCount = 0;
}

StackSnapshotsBufferManager::~StackSnapshotsBufferManager()
{
    std::lock_guard<std::recursive_mutex> guardedLock(_modificationLock);

    _current->Release();
    for (auto segment : _freeSegments)
    {
        segment->Release();
    }
    for (auto segment : _pendingSegments)
    {
        segment->Release();
    }
}

const char* StackSnapshotsBufferManager::GetName()
{
    return _serviceName;
}

bool StackSnapshotsBufferManager::Start()
{
    _completeAndEnqueueSegment_Worker.Start();
    return true;
}

bool StackSnapshotsBufferManager::Stop()
{
    // nothing to stop
    return true;
}

bool StackSnapshotsBufferManager::TryCompleteCurrentWriteSegment()
{
    std::lock_guard<std::recursive_mutex> guardedLock(_modificationLock);

    // This method is only called from managed code
    // --> TryCompleteAndEnqueueForExport cannot fails so no need to keep track of the current segment in pendings
    NotifyForExport_Offload(_current);

    // the current segment cannot be used any more so a new one is needed
    _current = GetSegment();

    return true;
}

bool StackSnapshotsBufferManager::TryMakeSegmentAvailableForWrite(StackSnapshotsBufferSegment* segment)
{
    std::lock_guard<std::recursive_mutex> guardedLock(_modificationLock);

    // We must call AddRef now, because TryResetForWrite() calls Release to mark that there is no more ref from managed code.
    // However, if we jump out of this funtion below before adding to that array, we must Release.
    segment->AddRef();

    if (!segment->TryResetForWrite())
    {
        segment->Release();

        Log::Error("StackSnapshotsBufferManager: impossible to 'reset for write' a segment");
        return false;
    }

    // add it to the free list
    _freeSegments.push_back(segment);
    _segmentsInManagedCount--;
    return true;
}

bool StackSnapshotsBufferManager::TooManySegmentsInMemory()
{
    return (_segmentsInManagedCount + _pendingSegments.size() + 1 >= BufferSegmentsCountMax);
}

void StackSnapshotsBufferManager::Add(const StackSnapshotResultBuffer* pSnapshotResult, const ManagedThreadInfo* pThreadInfo)
{
    std::lock_guard<std::recursive_mutex> guardedLock(_modificationLock);

    // avoid consuming too much memory by limiting the number of segments sent to managed
    if (TooManySegmentsInMemory())
    {
        return;
    }

    // this fails if the segment is too small to contain the new snapshot
    if (_current->TryAddSnapshot(pSnapshotResult, pThreadInfo))
    {
        return;
    }

    // current segment is full and should be sent to managed side


    // check against native code calling managed before it has been started (i.e. the reverse P/Invoke callbacks are not set yet)
    if (!NotifyForExport_Offload(_current))
    {
        // store the current segment into the "pending" list
        _pendingSegments.push_back(_current);
    }
    else
    {
        // try to send the pending segments if any
        SendPendingSegments();
    }

    // get a new one to store the snapshot result
    _current = GetSegment();
    _current->TryAddSnapshot(pSnapshotResult, pThreadInfo);

    // keep track of crossing the memory threshold
    if (TooManySegmentsInMemory())
    {
        Log::Error("Memory threshold reached - (", BufferSegmentsCountMax, ") segments sent to profile generation: stop accepting stack snapshots.");
    }
}

void StackSnapshotsBufferManager::SendPendingSegments()
{
    for (auto segment : _pendingSegments)
    {
        NotifyForExport_Offload(segment);
    }

    _pendingSegments.clear();
}

void StackSnapshotsBufferManager::ToString(time_t& unixTime, char* buffer, size_t bufferLen) const
{
    struct tm timeBuffer;

#if _WINDOWS
    gmtime_s(&timeBuffer, &unixTime);
    asctime_s(buffer, bufferLen, &timeBuffer);

    // remove \n at the end of the string
    auto lineLength = strlen(buffer);
    char* pEndOfLine = &buffer[lineLength - 1];
    if (*pEndOfLine == '\n')
    {
        *pEndOfLine = '\0';
    }
#else
    gmtime_r(&unixTime, &timeBuffer);
    asctime_r(&timeBuffer, buffer);
#endif
}

bool StackSnapshotsBufferManager::NotifyForExport_Offload(StackSnapshotsBufferSegment* segment)
{
    // When a native thread enters a reverse p/invoke operation,
    // the CLR identify it as a managed thread (i.e. the ICorProfilerCallback::ThreadCreated is called)
    // --> it means that we could try to suspend it in order to walk its stack
    // --> we don't want that for the sampler thread !!!
    // So the offload thread is used for the reverse p/invoke operation
    //
    bool sentToManaged;
    _completeAndEnqueueSegment_Worker.ExecuteWorkItem(segment, &sentToManaged);

    // keep track that a segment has been sent (or not) to managed
    time_t startTime = segment->GetStartTime();
    char szStartTime[32];
    time_t endTime = segment->GetEndTime();
    char szEndTime[32];
    ToString(startTime, szStartTime, sizeof szStartTime);
    ToString(endTime, szEndTime, sizeof szEndTime);

    if (sentToManaged)
    {
        _segmentsInManagedCount++;
        segment->Release();

        Log::Info("Segment (", segment->GetSnapshotsCount(), " snapshots from ", szStartTime, " to ", szEndTime, ") sent to managed");
    }
    else
    {
        Log::Info("Impossible to send segment (", segment->GetSnapshotsCount(), " snapshots from ", szStartTime, " to ", szEndTime, ") to managed");
    }

    return sentToManaged;
}

StackSnapshotsBufferSegment* StackSnapshotsBufferManager::GetSegment()
{
    // !! must be called under _modificationLock lock !!

    StackSnapshotsBufferSegment* segment = nullptr;

    // look for a free segment (i.e. has been disposed by managed code)
    if (!_freeSegments.empty())
    {
        // use the last free segment as current and remove it from the free list
        segment = _freeSegments.back();
        _freeSegments.pop_back();
    }
    else // otherwise, new up a segment
    {
        segment = new StackSnapshotsBufferSegment(StackSnapshotsBufferManager::BufferSegmentSizeBytes, _pSymbolsResolver);
        segment->AddRef();
    }

    return segment;
}


const char* StackSnapshotsBufferManager::CompleteAndEnqueueSegmentForExport_OffThreadWorker::ManagedThreadName = "DD.Profiler.CompleteAndEnqueueSegmentForExport_OffThreadWorker";
const WCHAR* StackSnapshotsBufferManager::CompleteAndEnqueueSegmentForExport_OffThreadWorker::NativeThreadName = WStr("DD.Profiler.CompleteAndEnqueueSegmentForExport_OffThreadWorker");

StackSnapshotsBufferManager::CompleteAndEnqueueSegmentForExport_OffThreadWorker::CompleteAndEnqueueSegmentForExport_OffThreadWorker(
    StackSnapshotsBufferManager* pOwner, IThreadsCpuManager* pThreadsCpuManager
    ) :
    SynchronousOffThreadWorkerBase(pThreadsCpuManager),
    _pOwner{pOwner}
{
}

StackSnapshotsBufferManager::CompleteAndEnqueueSegmentForExport_OffThreadWorker::~CompleteAndEnqueueSegmentForExport_OffThreadWorker()
{
    _pOwner = nullptr;
}

bool StackSnapshotsBufferManager::CompleteAndEnqueueSegmentForExport_OffThreadWorker::ShouldInitializeCurrentThreadforManagedInteractions(
    ICorProfilerInfo4** ppCorProfilerInfo)
{
    return false;
}

bool StackSnapshotsBufferManager::CompleteAndEnqueueSegmentForExport_OffThreadWorker::ShouldSetManagedThreadName(const char** managedThreadName)
{
    if (managedThreadName != nullptr)
    {
        *managedThreadName = StackSnapshotsBufferManager::CompleteAndEnqueueSegmentForExport_OffThreadWorker::ManagedThreadName;
    }

    return true;
}

bool StackSnapshotsBufferManager::CompleteAndEnqueueSegmentForExport_OffThreadWorker::ShouldSetNativeThreadName(const WCHAR** nativeThreadName)
{
    if (nativeThreadName != nullptr)
    {
        *nativeThreadName = StackSnapshotsBufferManager::CompleteAndEnqueueSegmentForExport_OffThreadWorker::NativeThreadName;
    }

    return true;
}

void StackSnapshotsBufferManager::CompleteAndEnqueueSegmentForExport_OffThreadWorker::PerformWork(void* pParameters, void* pResults)
{
    StackSnapshotsBufferSegment* pSegment = static_cast<StackSnapshotsBufferSegment*>(pParameters);
    bool* isCompletedAndEnqueued = static_cast<bool*>(pResults);

    if (pSegment != nullptr)
    {
        bool result = pSegment->TryCompleteAndEnqueueForExport();

        if (isCompletedAndEnqueued != nullptr)
        {
            *isCompletedAndEnqueued = result;
        }
    }
}
