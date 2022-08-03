// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once
#include "cor.h"
#include "corprof.h"

#include "StackFramesCollectorBase.h"

// forward declarations
namespace shared {
struct LoaderResourceMonikerIDs;
}

class StackSnapshotResultReusableBuffer;
class IManagedThreadList;

// Those functions must be defined in the main projects (Linux and Windows)
// Here are forward declarations to avoid hard coupling
namespace OsSpecificApi {
std::unique_ptr<StackFramesCollectorBase> CreateNewStackFramesCollectorInstance(ICorProfilerInfo4* pCorProfilerInfo);
uint64_t GetThreadCpuTime(ManagedThreadInfo* pThreadInfo);
bool IsRunning(ManagedThreadInfo* pThreadInfo, uint64_t& cpuTime);
}