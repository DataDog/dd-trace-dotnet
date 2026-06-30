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
class IRuntimeInfo;
class INativeHeapEnumerator;
class MetricsRegistry;
class ProxyMetric;

// Service that produces eeheap.json. On first use it lazily builds the right native-heap backend
// selected by the runtime version (cDAC for .NET 11+, DAC otherwise), then on every export
// enumerates the CLR native heaps and serializes them to JSON. Enumeration is timed and exposed
// both as a log line and as the dotnet_eeheap_duration metric.
class EEHeapReporter : public IEEHeapReporter, public ServiceBase
{
public:
    EEHeapReporter(IConfiguration* pConfiguration, IRuntimeInfo* pRuntimeInfo, MetricsRegistry& metricsRegistry);
    ~EEHeapReporter() override;

    // IEEHeapReporter
    std::string GetAndClearEEHeapContent() override;

public: // exposed for unit tests
    // Backend selection rule: .NET 11+ (and not .NET Framework) -> cDAC; everything earlier -> DAC.
    static bool ShouldUseCdac(IRuntimeInfo* pRuntimeInfo);

    // Serializes native-heap records to the eeheap.json shape.
    static std::string ToJson(const char* source, const std::vector<ClrNativeHeapInfo>& heaps);

    // Injects a (fake) backend, bypassing the version-based factory. For tests only.
    void InjectEnumeratorForTest(std::unique_ptr<INativeHeapEnumerator> enumerator, const char* backendName);

protected:
    const char* GetName() override
    {
        return "EEHeapReporter";
    }

    // ServiceBase
    bool StartImpl() override;
    bool StopImpl() override;

private:
    void EnsureBackendCreated();

    IConfiguration* _pConfiguration;
    IRuntimeInfo* _pRuntimeInfo;

    std::mutex _lock;
    bool _backendCreated = false;
    std::unique_ptr<INativeHeapEnumerator> _enumerator;
    const char* _backendName = "none";

    // Last enumeration duration in milliseconds, surfaced via dotnet_eeheap_duration.
    uint64_t _duration = 0;
    std::shared_ptr<ProxyMetric> _durationMetric;
};
