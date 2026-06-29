// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "EEHeapReporter.h"

#include "CdacNativeHeapEnumerator.h"
#include "ClrNativeHeapInfo.h"
#include "DacNativeHeapEnumerator.h"
#include "IConfiguration.h"
#include "INativeHeapEnumerator.h"
#include "IRuntimeInfo.h"
#include "Log.h"
#include "MetricsRegistry.h"
#include "OpSysTools.h"
#include "ProxyMetric.h"

#include <sstream>

EEHeapReporter::EEHeapReporter(IConfiguration* pConfiguration, IRuntimeInfo* pRuntimeInfo, MetricsRegistry& metricsRegistry) :
    _pConfiguration{pConfiguration},
    _pRuntimeInfo{pRuntimeInfo}
{
    _durationMetric = metricsRegistry.GetOrRegister<ProxyMetric>("dotnet_eeheap_duration", [this]() {
        return static_cast<double>(_duration);
    });
}

EEHeapReporter::~EEHeapReporter() = default;

bool EEHeapReporter::StartImpl()
{
    return true;
}

bool EEHeapReporter::StopImpl()
{
    return true;
}

bool EEHeapReporter::ShouldUseCdac(IRuntimeInfo* pRuntimeInfo)
{
    return (pRuntimeInfo != nullptr) &&
           !pRuntimeInfo->IsDotnetFramework() &&
           (pRuntimeInfo->GetMajorVersion() >= 11);
}

void EEHeapReporter::InjectEnumeratorForTest(std::unique_ptr<INativeHeapEnumerator> enumerator, const char* backendName)
{
    std::lock_guard<std::mutex> lock(_lock);
    _backendCreated = true;
    _backendName = backendName;
    _enumerator = std::move(enumerator);
}

void EEHeapReporter::EnsureBackendCreated()
{
    if (_backendCreated)
    {
        return;
    }
    _backendCreated = true;

    // .NET 11+ (and not .NET Framework) -> cDAC contracts; everything earlier -> the DAC.
    if (ShouldUseCdac(_pRuntimeInfo))
    {
        _backendName = "cdac";
        _enumerator = std::make_unique<CdacNativeHeapEnumerator>();
    }
    else
    {
        _backendName = "dac";
        _enumerator = std::make_unique<DacNativeHeapEnumerator>(_pRuntimeInfo);
    }

    if (_enumerator == nullptr || !_enumerator->IsAvailable())
    {
        Log::Info("!eeheap (", _backendName, "): native-heap backend unavailable; no eeheap.json will be produced.");
        _enumerator.reset();
    }
}

namespace
{
std::string ToHex(uintptr_t value)
{
    std::stringstream ss;
    ss << "0x" << std::hex << value;
    return ss.str();
}
} // namespace

std::string EEHeapReporter::ToJson(const char* source, const std::vector<ClrNativeHeapInfo>& heaps)
{
    std::stringstream ss;
    ss << "{\"source\":\"" << source << "\",\"heaps\":[";

    bool first = true;
    for (const auto& heap : heaps)
    {
        if (!first)
        {
            ss << ",";
        }
        first = false;

        ss << "{\"address\":\"" << ToHex(heap.Address) << "\""
           << ",\"size\":" << heap.Size
           << ",\"committed\":" << heap.Committed
           << ",\"kind\":\"" << ToString(heap.Kind) << "\""
           << ",\"state\":\"" << ToString(heap.State) << "\"";
        if (heap.GCHeap >= 0)
        {
            ss << ",\"gc_heap\":" << heap.GCHeap;
        }
        if (heap.Generation >= 0)
        {
            ss << ",\"generation\":" << heap.Generation;
        }
        ss << "}";
    }

    ss << "]}";
    return ss.str();
}

std::string EEHeapReporter::GetAndClearEEHeapContent()
{
    std::lock_guard<std::mutex> lock(_lock);

    EnsureBackendCreated();
    if (_enumerator == nullptr)
    {
        return std::string{};
    }

    const auto start = OpSysTools::GetHighPrecisionTimestamp();
    std::vector<ClrNativeHeapInfo> heaps = _enumerator->EnumerateAll();
    const auto elapsed = OpSysTools::GetHighPrecisionTimestamp() - start;
    _duration = static_cast<uint64_t>(elapsed.count() / 1000000);

    Log::Info("!eeheap (", _backendName, "): enumerated ", heaps.size(), " native heaps in ", _duration, " ms.");

    if (heaps.empty())
    {
        return std::string{};
    }

    return ToJson(_backendName, heaps);
}
