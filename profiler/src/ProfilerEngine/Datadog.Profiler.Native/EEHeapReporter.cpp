// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "EEHeapReporter.h"

#include "ClrNativeHeapInfo.h"
#include "IClrNativeHeapSnapshot.h"
#include "IConfiguration.h"
#include "Log.h"
#include "MetricsRegistry.h"
#include "OpSysTools.h"
#include "ProxyMetric.h"

#include <sstream>

EEHeapReporter::EEHeapReporter(IConfiguration* pConfiguration, IClrNativeHeapSnapshot* pSnapshot, MetricsRegistry& metricsRegistry) :
    _pConfiguration{pConfiguration},
    _pSnapshot{pSnapshot}
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
           << ",\"group\":\"" << ToString(GroupOf(heap.Kind)) << "\""
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

    if (_pSnapshot == nullptr || !_pSnapshot->IsAvailable())
    {
        return std::string{};
    }

    const char* backendName = _pSnapshot->GetBackendName();

    const auto start = OpSysTools::GetHighPrecisionTimestamp();
    const std::vector<ClrNativeHeapInfo>& heaps = _pSnapshot->GetSnapshot();
    const auto elapsed = OpSysTools::GetHighPrecisionTimestamp() - start;
    _duration = static_cast<uint64_t>(elapsed.count() / 1000000);

    Log::Info("!eeheap (", backendName, "): enumerated ", heaps.size(), " native heaps in ", _duration, " ms.");

    if (heaps.empty())
    {
        return std::string{};
    }

    return ToJson(backendName, heaps);
}
