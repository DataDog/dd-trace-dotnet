// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "IService.h"
#include "MetricsRegistry.h"
#include "CounterMetric.h"

#include <memory>

class IConfiguration;
struct ManagedThreadInfo;

class SystemCallsShield : public IService
{
public:
    SystemCallsShield(IConfiguration* configurationMetrics);

    void Register(std::shared_ptr<ManagedThreadInfo> const& threadInfo);
    void Unregister();

    bool Start() override;
    bool Stop() override;

    const char* GetName() override;


private:
    static SystemCallsShield* Instance;
    static int SetSharedMemory(volatile int* state);
    static bool ShouldEnable(IConfiguration* configuration);

    int SetSharedMemoryOnThreadInfo(volatile int* state);

    bool _isEnabled;
};
