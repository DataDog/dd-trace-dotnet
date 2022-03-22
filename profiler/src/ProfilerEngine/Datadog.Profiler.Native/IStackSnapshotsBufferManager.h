// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "IService.h"
#include "ManagedThreadInfo.h"

// forward declarations
class StackSnapshotResultBuffer;
class StackSnapshotsBufferSegment;


class IStackSnapshotsBufferManager : public IService
{
public:
    virtual void Add(const StackSnapshotResultBuffer* pSnapshotResult, const ManagedThreadInfo* pThreadInfo) = 0;

    // methods called from managed code via P/Invoke
    virtual bool TryCompleteCurrentWriteSegment() = 0;
    virtual bool TryMakeSegmentAvailableForWrite(StackSnapshotsBufferSegment* segment) = 0;
};