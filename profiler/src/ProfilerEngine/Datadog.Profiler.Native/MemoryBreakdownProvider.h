// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "ClrNativeHeapInfo.h"
#include "ISamplesProvider.h"
#include "SampleValueTypeProvider.h"

#include <cstdint>
#include <deque>
#include <map>
#include <memory>
#include <string>
#include <vector>

class IClrNativeHeapSnapshot;
class IAddressSpaceMap;
class MetricsRegistry;
class ProxyMetric;
struct AddressRegion;

// Process-level, once-per-export sample provider that reconciles the OS address-space map with the
// CLR native/managed heap snapshot (DAC/cDAC) into a single non-double-counted memory flamegraph.
//
// Two sample value types are produced: "committed" (Windows MEM_COMMIT; 0 on Linux) and "rss"
// (Linux smaps Rss always; Windows working set only when its option is enabled). Frames are synthetic
// (Process Memory -> group -> leaf); managed heap detail is split by generation (gen0/1/2/LOH/POH).
class MemoryBreakdownProvider : public ISamplesProvider
{
public:
    MemoryBreakdownProvider(SampleValueTypeProvider& valueTypeProvider, IClrNativeHeapSnapshot* pSnapshot, MetricsRegistry& metricsRegistry);
    ~MemoryBreakdownProvider() override;

    // ISamplesProvider
    std::unique_ptr<SamplesEnumerator> GetSamples() override;
    const char* GetName() override;

public: // exposed for unit tests
    static std::vector<SampleValueType> SampleTypeDefinitions;

private:
    // Which top-level group / memory_source a leaf belongs to.
    enum class Source
    {
        Managed,
        ClrNative,
        Image,
        MappedFile,
        Private,
        Stack,
        Reserved,
    };

    // One aggregated leaf (collapses across heaps for a generation, and across protection runs for a
    // module). Summed committed/rss over the whole export.
    struct LeafInfo
    {
        Source source = Source::Private;
        NativeHeapKind kind = NativeHeapKind::Unknown;
        NativeHeapState state = NativeHeapState::None;
        int generation = -1;
        std::string moduleName; // image module leaf / mapped-file name
        uint64_t clrViewCommitted = 0; // sum of ClrNativeHeapInfo.Committed (cross-check label)
        uint64_t committed = 0;
        uint64_t rss = 0;
        bool rssEstimated = false;
    };

    // A disjoint CLR interval [Start, End) attributed to exactly one heap (after clipping overlaps).
    struct ClrInterval
    {
        uint64_t Start = 0;
        uint64_t End = 0;
        const ClrNativeHeapInfo* Info = nullptr;
    };

    std::vector<ClrInterval> BuildDisjointClrIntervals(const std::vector<ClrNativeHeapInfo>& heaps) const;
    void Reconcile(const std::vector<AddressRegion>& regions,
                   const std::vector<ClrInterval>& intervals,
                   bool providesRss);

    void AccumulateClrSlice(const ClrNativeHeapInfo& info, uint64_t committed, uint64_t rss, bool estimated);
    void AccumulateOsRemainder(const AddressRegion& region, uint64_t committed, uint64_t rss);

    static std::string LeafKeyForClr(const ClrNativeHeapInfo& info, Source& outSource);
    static std::string LeafKeyForOs(const AddressRegion& region, Source& outSource);

    IClrNativeHeapSnapshot* _pSnapshot;
    std::vector<SampleValueTypeProvider::Offset> _valueOffsets;
    size_t _committedOffset = 0;
    size_t _rssOffset = 0;

    // Rebuilt every GetSamples(): backing store for dynamic (per-module) frame strings so the
    // string_views handed to FrameInfoView stay valid for the whole Export().
    std::deque<std::string> _frameStrings;
    std::map<std::string, LeafInfo> _leaves;

    uint64_t _duration = 0;           // dotnet_memory_breakdown_duration (ms)
    uint64_t _workingSetDuration = 0; // dotnet_memory_breakdown_workingset_duration (ms)
    std::shared_ptr<ProxyMetric> _durationMetric;
    std::shared_ptr<ProxyMetric> _workingSetDurationMetric;
};
