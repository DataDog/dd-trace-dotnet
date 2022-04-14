// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once
#include <list>
#include <mutex>
#include <string>
#include <thread>

#include "CollectorBase.h"
#include "RawWallTimeSample.h"

// forward declarations
class IConfiguration;
class IFrameStore;
class IAppDomainStore;
class IRuntimeIdStore;

class WallTimeProvider
    : public CollectorBase<RawWallTimeSample> // accepts raw walltime samples
{
public:
    WallTimeProvider(
        IConfiguration* pConfiguration,
        IFrameStore* pFrameStore,
        IAppDomainStore* pAssemblyStore,
        IRuntimeIdStore* pRuntimeIdStore
        );

// interfaces implementation
public:
    const char* GetName() override;

private:
    virtual void OnTransformRawSample(const RawWallTimeSample& rawSample, Sample& sample) override;

private:
    const char* _serviceName = "WallTimeProvider";
};
