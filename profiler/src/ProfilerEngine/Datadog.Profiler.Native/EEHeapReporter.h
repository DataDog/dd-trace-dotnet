// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "ClrNativeHeapInfo.h"
#include "IEEHeapReporter.h"
#include "ServiceBase.h"

#include <cstdint>
#include <memory>
#include <mutex>
#include <string>
#include <vector>

class IConfiguration;
class IClrNativeHeapSnapshot;
class MetricsRegistry;
class ProxyMetric;

// Service that produces eeheap.json. It sources the CLR native heaps from the shared, per-export
// ClrNativeHeapSnapshot (which owns the DAC/cDAC backend and the OS address-space map), then on
// every export serializes them to JSON. Enumeration is timed and exposed both as a log line and as
// the dotnet_eeheap_duration metric.
class EEHeapReporter : public IEEHeapReporter, public ServiceBase
{
public:
    EEHeapReporter(IConfiguration* pConfiguration, IClrNativeHeapSnapshot* pSnapshot, MetricsRegistry& metricsRegistry);
    ~EEHeapReporter() override;

    // IEEHeapReporter
    std::string GetAndClearEEHeapContent() override;

public: // exposed for unit tests
    // Serializes native-heap records to the eeheap.json shape.
    static std::string ToJson(const char* source, const std::vector<ClrNativeHeapInfo>& heaps);

protected:
    const char* GetName() override
    {
        return "EEHeapReporter";
    }

    // ServiceBase
    bool StartImpl() override;
    bool StopImpl() override;

private:
    IConfiguration* _pConfiguration;
    IClrNativeHeapSnapshot* _pSnapshot;

    std::mutex _lock;

    // Last enumeration duration in milliseconds, surfaced via dotnet_eeheap_duration.
    uint64_t _duration = 0;
    std::shared_ptr<ProxyMetric> _durationMetric;
};
