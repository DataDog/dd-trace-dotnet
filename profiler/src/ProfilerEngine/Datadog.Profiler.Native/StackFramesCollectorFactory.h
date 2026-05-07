// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <memory>

struct ICorProfilerInfo4;
class IConfiguration;
class CallstackProvider;
class MetricsRegistry;
class StackFramesCollectorBase;

class StackFramesCollectorFactory
{
public:
    StackFramesCollectorFactory(
        ICorProfilerInfo4* pCorProfilerInfo,
        IConfiguration const* pConfiguration,
        MetricsRegistry& metricsRegistry);
    ~StackFramesCollectorFactory() = default;

    std::unique_ptr<StackFramesCollectorBase> Create(CallstackProvider* callstackProvider);

private:
    ICorProfilerInfo4* _pCorProfilerInfo;
    IConfiguration const* _pConfiguration;
    MetricsRegistry& _metricsRegistry;
};