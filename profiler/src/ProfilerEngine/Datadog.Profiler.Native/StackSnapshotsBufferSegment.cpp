// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "StackSnapshotsBufferSegment.h"
#include "Log.h"
#include "PInvoke.h"
#include "SymbolsResolver.h"

#include "shared/src/native-src/string.h"

StackSnapshotsBufferSegment::StackSnapshotsBufferSegment(std::uint32_t size) :
    _segmentBytes{nullptr},
    _segmentSize{0},
    _nextFreeByteOffset{0},
    _snapshotsCount{0},
    _unixTimeUtcRangeStart{0},
    _unixTimeUtcRangeEnd{0},
    _status{static_cast<std::uint8_t>(StackSnapshotsBufferSegment::StatusCode::Unknown)}
{
    if (size > StackSnapshotsBufferSegment::MaxSegmentSize)
    {
        size = StackSnapshotsBufferSegment::MaxSegmentSize;
    }

    _segmentBytes = malloc(size);
    _segmentSize = (_segmentBytes == nullptr) ? 0 : size;

    SetStatus(StackSnapshotsBufferSegment::StatusCode::IsAvailableForWrite);
}

StackSnapshotsBufferSegment::~StackSnapshotsBufferSegment()
{
    std::lock_guard<std::mutex> guardedLock(_modificationLock);

    void* segmentBytes = _segmentBytes;
    if (segmentBytes != nullptr)
    {
        SetStatus(StackSnapshotsBufferSegment::StatusCode::Unknown);
        _segmentBytes = nullptr;
        _segmentSize = 0;
        _nextFreeByteOffset = 0;
        _snapshotsCount = 0;
        _unixTimeUtcRangeStart = 0;
        _unixTimeUtcRangeEnd = 0;

        free(segmentBytes);
    }
}

bool StackSnapshotsBufferSegment::TryAddSnapshot(const StackSnapshotResultBuffer* pSnapshotResult, const ManagedThreadInfo* pThreadInfo)
{
    if (GetStatus() != StackSnapshotsBufferSegment::StatusCode::IsAvailableForWrite)
    {
        return false;
    }

    {
        std::lock_guard<std::mutex> guardedLock(_modificationLock);

        if (GetStatus() != StackSnapshotsBufferSegment::StatusCode::IsAvailableForWrite)
        {
            return false;
        }

        std::uint32_t reqBytes = StackSnapshotResult::GetRequiredBufferBytes(pSnapshotResult);

        if (_nextFreeByteOffset + reqBytes > _segmentSize)
        {
            // Theoretically, another snapshot with fewer frames could fit later.
            // However, a segment typically holds a large number of snapshots and particularly large outliers are rare.
            // So we are OK with looking a couple of bytes here for the same of simplicity and performance, so we
            // will not permitt adding later snapshots once a snapshot did not fit.

            SetStatus(StackSnapshotsBufferSegment::StatusCode::IsFull);
            return false;
        }

        StackSnapshotResult shapshot(GetPointerToOffset(_nextFreeByteOffset));
        _nextFreeByteOffset += shapshot.WriteData(pSnapshotResult, pThreadInfo);

        _snapshotsCount++;

        std::uint64_t snapshotTimestamp = pSnapshotResult->GetUnixTimeUtc();

        if (_unixTimeUtcRangeStart == 0 || snapshotTimestamp < _unixTimeUtcRangeStart)
        {
            _unixTimeUtcRangeStart = snapshotTimestamp;
        }

        if (_unixTimeUtcRangeEnd == 0 || snapshotTimestamp > _unixTimeUtcRangeEnd)
        {
            _unixTimeUtcRangeEnd = snapshotTimestamp;
        }
    }

    return true;
}

bool StackSnapshotsBufferSegment::TryCompleteAndEnqueueForExport(void)
{
    std::lock_guard<std::mutex> guardedLock(_modificationLock);

    StatusCode status = GetStatus();
    if (status != StatusCode::IsAvailableForWrite && status != StatusCode::IsFull)
    {
        return false;
    }

    SetStatus(StatusCode::IsReservedForRead);

    HRESULT hr;
    if (ManagedCallbackRegistry::EnqueueStackSnapshotBufferSegmentForExport::TryInvoke(this,
                                                                                       _segmentBytes,
                                                                                       _nextFreeByteOffset,
                                                                                       _snapshotsCount,
                                                                                       _unixTimeUtcRangeStart,
                                                                                       _unixTimeUtcRangeEnd,
                                                                                       &hr))
    {
        if (SUCCEEDED(hr))
        {
            // This represents that managed code now has a pointer to this segment:
            // (We need to call release when resetting for write.)
            this->AddRef();

            return true;
        }
    }

    SetStatus(status);
    return false;
}

bool StackSnapshotsBufferSegment::TryResetForWrite(void)
{
    std::lock_guard<std::mutex> guardedLock(_modificationLock);

    StatusCode status = GetStatus();
    if (status != StatusCode::IsReservedForRead)
    {
        return false;
    }

    // This represents that managed code no longer has a pointer to this segment:
    this->Release();

    _nextFreeByteOffset = 0;
    _snapshotsCount = 0;

    _unixTimeUtcRangeStart = 0;
    _unixTimeUtcRangeEnd = 0;

    SetStatus(StatusCode::IsAvailableForWrite);
    return true;
}

StackSnapshotResult StackSnapshotsBufferSegment::ReadFirstSnapshot() const
{
    if (_segmentSize == 0 || _snapshotsCount == 0 || GetStatus() != StackSnapshotsBufferSegment::StatusCode::IsReservedForRead)
    {
        return StackSnapshotResult::InvalidSnapshot;
    }

    return StackSnapshotResult(_segmentBytes);
}

StackSnapshotResult StackSnapshotsBufferSegment::ReadNextSnapshot(StackSnapshotResult stackSnapshot) const
{
    if (_segmentSize == 0 || _snapshotsCount == 0 || GetStatus() != StackSnapshotsBufferSegment::StatusCode::IsReservedForRead)
    {
        return StackSnapshotResult::InvalidSnapshot;
    }

    void* pNextSnapshotMem = stackSnapshot.GetNextSnapshotMemoryPointer();
    if (pNextSnapshotMem == nullptr)
    {
        return StackSnapshotResult::InvalidSnapshot;
    }

    std::uint32_t offset = GetOffsetOfPointer(pNextSnapshotMem);
    if (offset == StackSnapshotsBufferSegment::InvalidOffsetMarker)
    {
        return StackSnapshotResult::InvalidSnapshot;
    }

    if (_nextFreeByteOffset <= offset)
    {
        return StackSnapshotResult::InvalidSnapshot;
    }

    return StackSnapshotResult(pNextSnapshotMem);
}

void StackSnapshotsBufferSegment::DebugDumpAllSnapshots(void) const
{
    Log::Debug("StackSnapshotsBufferSegment::DebugDumpAllSnapshots(): _status=", static_cast<std::uint8_t>(_status),
               ", _segmentBytes(ptr)=", reinterpret_cast<std::uint64_t>(_segmentBytes),
               ", _snapshotsCount=", this->GetSnapshotsCount());

    std::uint32_t snapshotNr = 0;
    StackSnapshotResult snapshot = this->ReadFirstSnapshot();
    while (snapshot.IsValid())
    {
        snapshotNr++;
        Log::Debug("\n SNAPSHOT ", snapshotNr, "/", this->GetSnapshotsCount(),
                   ": RepresentedNanosecs=", snapshot.GetRepresentedDurationNanoseconds(),
                   ", ProfilerThreadInfoId=", snapshot.GetProfilerThreadInfoId(),
                   ", FramesCount=", snapshot.GetFramesCount());

        StackFrameCodeKind codeKind;
        std::uint64_t frameInfoCode;
        shared::WSTRING frameDisplay;
        std::uint16_t framesCount = snapshot.GetFramesCount();
        for (std::uint16_t f = 0; f < framesCount; f++)
        {
            if (snapshot.TryGetFrameAtIndex(f, &codeKind, &frameInfoCode))
            {
                StackSnapshotResultFrameInfo frameInfo(codeKind, frameInfoCode);
                StackFrameInfo* resolvedFrame;

                SymbolsResolver::GetSingletonInstance()->ResolveStackFrameSymbols(frameInfo, &resolvedFrame, true);
                if (resolvedFrame != nullptr)
                {
                    frameDisplay.clear();
                    resolvedFrame->ToDisplayString(&frameDisplay);
                    Log::Debug(frameDisplay);
                }
            }
        }

        snapshot = this->ReadNextSnapshot(snapshot);
    }
}
