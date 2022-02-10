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

// Those functions must be defined in the main projects (Linux and Windows)
// Here are forward declarations to avoid hard coupling
namespace OsSpecificApi {
void InitializeLoaderResourceMonikerIDs(shared::LoaderResourceMonikerIDs* moniker);

StackFramesCollectorBase* CreateNewStackFramesCollectorInstance(ICorProfilerInfo4* pCorProfilerInfo);
}