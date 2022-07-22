// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "CollectorBase.h"
#include "RawWallTimeSample.h"

// forward declarations
class IConfiguration;
class IFrameStore;
class IAppDomainStore;
class IRuntimeIdStore;
class IThreadsCpuManager;


class WallTimeProvider
    : public CollectorBase<RawWallTimeSample> // accepts raw walltime samples
{
public:
    WallTimeProvider(
        IThreadsCpuManager* pThreadsCpuManager,
        IFrameStore* pFrameStore,
        IAppDomainStore* pAssemblyStore,
        IRuntimeIdStore* pRuntimeIdStore
        );
};
