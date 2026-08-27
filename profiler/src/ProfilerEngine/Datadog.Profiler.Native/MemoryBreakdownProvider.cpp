// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "MemoryBreakdownProvider.h"

#include "AddressRegion.h"
#include "IAddressSpaceMap.h"
#include "IClrNativeHeapSnapshot.h"
#include "Log.h"
#include "MemoryBreakdownFrames.h"
#include "MetricsRegistry.h"
#include "OpSysTools.h"
#include "ProxyMetric.h"
#include "Sample.h"
#include "SamplesEnumerator.h"

#include <algorithm>
#include <cstdint>

std::vector<SampleValueType> MemoryBreakdownProvider::SampleTypeDefinitions{
    {"memory-breakdown", "bytes", -1},
};

namespace
{
// provider-local label keys (owned string constants; string_view keys reference these)
const std::string MemorySourceLabel = "memory_source";
const std::string RegionKindLabel = "region_kind";
const std::string RegionGroupLabel = "region_group";
const std::string RegionStateLabel = "region_state";
const std::string ModuleLabel = "module";
const std::string MappedFileLabel = "mapped_file";
#ifdef LINUX
const std::string RssEstimatedLabel = "rss_estimated";
#endif

// reused generation string values ("0".."4"), matching runtime/ClrMD generation numbers.
const std::string GenerationValues[5] = {"0", "1", "2", "3", "4"};

const std::string SourceManaged = "managed";
const std::string SourceClrNative = "clr-native";
const std::string SourceImage = "image";
const std::string SourceMappedFile = "mapped-file";
const std::string SourcePrivate = "private";
const std::string SourceStack = "stack";
const std::string SourceReserved = "reserved";
#ifdef LINUX
const std::string TrueValue = "true";
#endif

// Portion of a uniform run's committed/rss attributable to `overlap` bytes.
uint64_t SliceOf(uint64_t metric, uint64_t overlap, uint64_t runSize)
{
    if (metric == 0 || overlap == 0 || runSize == 0)
    {
        return 0;
    }
    if (overlap >= runSize || metric >= runSize)
    {
        return metric < overlap ? metric : overlap;
    }
    return static_cast<uint64_t>(static_cast<double>(metric) * static_cast<double>(overlap) / static_cast<double>(runSize));
}

// Clip priority: higher wins the bytes it covers. A container region (RegionOfRegions - the GC's big
// reserved region that holds the segments) is lowest so concrete segments win and only its uncovered
// reserve is attributed to it.
int ClipPriority(const ClrNativeHeapInfo& h)
{
    if (h.State == NativeHeapState::RegionOfRegions)
    {
        return 0;
    }
    switch (GroupOf(h.Kind))
    {
        case NativeHeapGroup::GCFreeAndReserve:
            return 1;
        default:
            return 2;
    }
}

bool IsManagedGroup(NativeHeapGroup group)
{
    return group == NativeHeapGroup::GCObjectHeap ||
           group == NativeHeapGroup::GCBookkeepingAndHandles ||
           group == NativeHeapGroup::GCFreeAndReserve;
}
} // namespace

MemoryBreakdownProvider::MemoryBreakdownProvider(SampleValueTypeProvider& valueTypeProvider, IClrNativeHeapSnapshot* pSnapshot, MetricsRegistry& metricsRegistry) :
    _pSnapshot{pSnapshot}
{
    _memoryBreakdownOffset = valueTypeProvider.GetOrRegister(SampleTypeDefinitions)[0];

    _durationMetric = metricsRegistry.GetOrRegister<ProxyMetric>("dotnet_memory_breakdown_duration", [this]() {
        return static_cast<double>(_duration);
    });
}

MemoryBreakdownProvider::~MemoryBreakdownProvider() = default;

const char* MemoryBreakdownProvider::GetName()
{
    return "MemoryBreakdownProvider";
}

std::string MemoryBreakdownProvider::LeafKeyForClr(const ClrNativeHeapInfo& info, Source& outSource)
{
    const NativeHeapGroup group = GroupOf(info.Kind);

    if (!IsManagedGroup(group))
    {
        outSource = Source::ClrNative;
        return std::string("N:") + ToString(info.Kind);
    }

    outSource = Source::Managed;

    if (info.Kind == NativeHeapKind::GCHeapSegment && info.Generation >= 0 && info.Generation <= 4)
    {
        return std::string("M:gen:") + GenerationValues[info.Generation];
    }
    if (info.Kind == NativeHeapKind::NonGCHeap)
    {
        return "M:nongc";
    }
    if (group == NativeHeapGroup::GCFreeAndReserve)
    {
        return "M:free";
    }
    if (group == NativeHeapGroup::GCBookkeepingAndHandles)
    {
        return "M:book";
    }
    return "M:gcheap"; // GCHeapSegment with unknown generation (segment GC)
}

std::string MemoryBreakdownProvider::LeafKeyForOs(const AddressRegion& region, Source& outSource)
{
    switch (region.Category)
    {
        case RegionCategory::Image:
            outSource = Source::Image;
            return std::string("I:") + region.ModuleName;

        case RegionCategory::MappedFile:
            outSource = Source::MappedFile;
            return std::string("F:") + region.ModuleName;

        case RegionCategory::Stack:
            outSource = Source::Stack;
            return "S";

        case RegionCategory::Reserved:
        case RegionCategory::Free:
            outSource = Source::Reserved;
            return "R";

        case RegionCategory::PrivateData:
        case RegionCategory::Heap:
        default:
            outSource = Source::Private;
            return "P";
    }
}

std::vector<MemoryBreakdownProvider::ClrInterval> MemoryBreakdownProvider::BuildDisjointClrIntervals(const std::vector<ClrNativeHeapInfo>& heaps) const
{
    // Coordinate sweep: collect boundaries, then for each elementary cell pick the highest-priority
    // heap that covers it. Adjacent cells resolving to the same heap are merged.
    std::vector<uint64_t> coords;
    coords.reserve(heaps.size() * 2);
    for (const auto& h : heaps)
    {
        if (h.Size == 0 || h.Address == 0)
        {
            continue;
        }
        coords.push_back(static_cast<uint64_t>(h.Address));
        coords.push_back(static_cast<uint64_t>(h.Address) + h.Size);
    }

    std::sort(coords.begin(), coords.end());
    coords.erase(std::unique(coords.begin(), coords.end()), coords.end());

    std::vector<ClrInterval> result;
    if (coords.size() < 2)
    {
        return result;
    }

    for (size_t c = 0; c + 1 < coords.size(); ++c)
    {
        const uint64_t cellStart = coords[c];
        const uint64_t cellEnd = coords[c + 1];

        const ClrNativeHeapInfo* winner = nullptr;
        int bestPrio = -1;
        uint64_t bestSize = 0;
        for (const auto& h : heaps)
        {
            if (h.Size == 0 || h.Address == 0)
            {
                continue;
            }
            const uint64_t s = static_cast<uint64_t>(h.Address);
            const uint64_t e = s + h.Size;
            if (s <= cellStart && cellEnd <= e)
            {
                const int prio = ClipPriority(h);
                // Higher priority wins; at equal priority the smaller (more specific) region wins.
                if (prio > bestPrio || (prio == bestPrio && (winner == nullptr || h.Size < bestSize)))
                {
                    winner = &h;
                    bestPrio = prio;
                    bestSize = h.Size;
                }
            }
        }

        if (winner == nullptr)
        {
            continue;
        }

        if (!result.empty() && result.back().Info == winner && result.back().End == cellStart)
        {
            result.back().End = cellEnd; // merge adjacent cells of the same heap
        }
        else
        {
            result.push_back({cellStart, cellEnd, winner});
        }
    }

    return result;
}

void MemoryBreakdownProvider::AccumulateClrSlice(const ClrNativeHeapInfo& info, uint64_t committed, uint64_t rss, bool estimated)
{
    Source source = Source::Managed;
    const std::string key = LeafKeyForClr(info, source);

    auto& leaf = _leaves[key];
    leaf.source = source;
    leaf.kind = info.Kind;
    leaf.state = info.State;
    leaf.generation = (info.Kind == NativeHeapKind::GCHeapSegment) ? info.Generation : leaf.generation;
    leaf.committed += committed;
    leaf.rss += rss;
    leaf.rssEstimated = leaf.rssEstimated || estimated;
}

void MemoryBreakdownProvider::AccumulateOsRemainder(const AddressRegion& region, uint64_t committed, uint64_t rss)
{
    if (committed == 0 && rss == 0)
    {
        return;
    }

    Source source = Source::Private;
    const std::string key = LeafKeyForOs(region, source);

    auto& leaf = _leaves[key];
    leaf.source = source;
    if (leaf.moduleName.empty() && !region.ModuleName.empty())
    {
        leaf.moduleName = region.ModuleName;
    }
    leaf.committed += committed;
    leaf.rss += rss;
}

void MemoryBreakdownProvider::Reconcile(const std::vector<AddressRegion>& regions,
                                        const std::vector<ClrInterval>& intervals,
                                        bool providesRss)
{
    // Two-pointer merge over the two address-sorted lists.
    size_t ivStart = 0;
    for (const auto& region : regions)
    {
        const uint64_t regionStart = static_cast<uint64_t>(region.Address);
        const uint64_t regionEnd = region.End();
        if (region.Size == 0)
        {
            continue;
        }

        // Advance past intervals that end at/below the region start.
        while (ivStart < intervals.size() && intervals[ivStart].End <= regionStart)
        {
            ivStart++;
        }

        uint64_t clrCommitted = 0;
        uint64_t clrRss = 0;

        for (size_t i = ivStart; i < intervals.size() && intervals[i].Start < regionEnd; ++i)
        {
            const uint64_t s = std::max(intervals[i].Start, regionStart);
            const uint64_t e = std::min(intervals[i].End, regionEnd);
            if (e <= s)
            {
                continue;
            }

            const uint64_t overlap = e - s;
            const uint64_t c = SliceOf(region.Committed, overlap, region.Size);
            const uint64_t r = providesRss ? SliceOf(region.Rss, overlap, region.Size) : 0;

            const bool estimated = providesRss && region.Rss > 0 && overlap < region.Size;

            AccumulateClrSlice(*intervals[i].Info, c, r, estimated);
            clrCommitted += c;
            clrRss += r;
        }

        // Whatever the CLR did not claim in this OS region is attributed to the OS-category leaf. Using
        // (region total - attributed) makes the per-region sum exact (no rounding drift, no double count).
        const uint64_t remC = region.Committed > clrCommitted ? region.Committed - clrCommitted : 0;
        const uint64_t regionRss = providesRss ? region.Rss : 0;
        const uint64_t remR = regionRss > clrRss ? regionRss - clrRss : 0;
        AccumulateOsRemainder(region, remC, remR);
    }
}

std::unique_ptr<SamplesEnumerator> MemoryBreakdownProvider::GetSamples()
{
    class ListSamplesEnumerator : public SamplesEnumerator
    {
    public:
        std::size_t size() const override
        {
            return _samples.size();
        }

        bool MoveNext(std::shared_ptr<Sample>& sample) override
        {
            if (_pos >= _samples.size())
            {
                return false;
            }
            sample = _samples[_pos++];
            return true;
        }

        std::vector<std::shared_ptr<Sample>> _samples;
        std::size_t _pos = 0;
    };

    auto enumerator = std::make_unique<ListSamplesEnumerator>();

    if (_pSnapshot == nullptr)
    {
        return enumerator;
    }

    const auto start = OpSysTools::GetHighPrecisionTimestamp();

    IAddressSpaceMap* map = _pSnapshot->GetAddressSpaceMap();

    if (map == nullptr || !map->IsAvailable() || !_pSnapshot->IsAvailable())
    {
        _duration = static_cast<uint64_t>((OpSysTools::GetHighPrecisionTimestamp() - start).count() / 1000000);
        return enumerator;
    }

    const std::vector<ClrNativeHeapInfo>& heaps = _pSnapshot->GetSnapshot();

    _frameStrings.clear();
    _leaves.clear();

    const auto intervals = BuildDisjointClrIntervals(heaps);
    Reconcile(map->Regions(), intervals, map->ProvidesRss());

    for (const auto& [key, leaf] : _leaves)
    {
#ifdef _WINDOWS
        const auto memoryBreakdown = leaf.committed;
#elif defined(LINUX)
        const auto memoryBreakdown = leaf.rss;
#else
#error Unsupported platform
#endif

        if (memoryBreakdown == 0)
        {
            continue;
        }

        auto sample = std::make_shared<Sample>(std::chrono::nanoseconds(0), std::string_view{}, 8);
        sample->AddValue(static_cast<int64_t>(memoryBreakdown), _memoryBreakdownOffset);

        // Frames are emitted leaf-first: pprof/libdatadog treats locations[0] as the leaf and the last
        // location as the root. So each sample is built as leaf -> group, with the common Root frame
        // appended after the switch.
        switch (leaf.source)
        {
            case Source::Managed:
            {
                std::string_view leafFrame = membreakdown::GcHeap;
                if (leaf.kind == NativeHeapKind::GCHeapSegment && leaf.generation >= 0 && leaf.generation <= 4)
                {
                    switch (leaf.generation)
                    {
                        case 0: leafFrame = membreakdown::Gen0; break;
                        case 1: leafFrame = membreakdown::Gen1; break;
                        case 2: leafFrame = membreakdown::Gen2; break;
                        case 3: leafFrame = membreakdown::Loh; break;
                        case 4: leafFrame = membreakdown::Poh; break;
                        default: break;
                    }
                }
                else if (leaf.kind == NativeHeapKind::NonGCHeap)
                {
                    leafFrame = membreakdown::NonGc;
                }
                else if (GroupOf(leaf.kind) == NativeHeapGroup::GCFreeAndReserve)
                {
                    leafFrame = membreakdown::GcFree;
                }
                else if (GroupOf(leaf.kind) == NativeHeapGroup::GCBookkeepingAndHandles)
                {
                    leafFrame = membreakdown::GcBook;
                }
                sample->AddFrame({membreakdown::Module, leafFrame, "", 0});
                sample->AddFrame({membreakdown::Module, membreakdown::Managed, "", 0});

                sample->AddLabel(StringLabel{MemorySourceLabel, SourceManaged});
                if (leaf.kind == NativeHeapKind::GCHeapSegment && leaf.generation >= 0 && leaf.generation <= 4)
                {
                    sample->AddLabel(StringLabel{Sample::GarbageCollectionGenerationLabel, GenerationValues[leaf.generation]});
                }
                sample->AddLabel(StringLabel{RegionKindLabel, ToString(leaf.kind)});
                sample->AddLabel(StringLabel{RegionStateLabel, ToString(leaf.state)});
                break;
            }

            case Source::ClrNative:
            {
                std::string_view leafFrame = membreakdown::Code;
                const NativeHeapGroup group = GroupOf(leaf.kind);
                if (group == NativeHeapGroup::Loader)
                {
                    leafFrame = membreakdown::Loader;
                }
                else if (group == NativeHeapGroup::VirtualStubDispatch)
                {
                    leafFrame = membreakdown::Vsd;
                }
                sample->AddFrame({membreakdown::Module, leafFrame, "", 0});
                sample->AddFrame({membreakdown::Module, membreakdown::ClrNative, "", 0});

                sample->AddLabel(StringLabel{MemorySourceLabel, SourceClrNative});
                sample->AddLabel(StringLabel{RegionGroupLabel, ToString(group)});
                sample->AddLabel(StringLabel{RegionKindLabel, ToString(leaf.kind)});
                sample->AddLabel(StringLabel{RegionStateLabel, ToString(leaf.state)});
                break;
            }

            case Source::Image:
            {
                if (!leaf.moduleName.empty())
                {
                    _frameStrings.push_back(std::string("|lm: |ns: |ct: |cg: |fn:") + leaf.moduleName + " |fg: |sg:");
                    sample->AddFrame({membreakdown::Module, _frameStrings.back(), "", 0});
                }
                sample->AddFrame({membreakdown::Module, membreakdown::Modules, "", 0});
                sample->AddLabel(StringLabel{MemorySourceLabel, SourceImage});
                if (!leaf.moduleName.empty())
                {
                    sample->AddLabel(StringLabel{ModuleLabel, leaf.moduleName});
                }
                break;
            }

            case Source::MappedFile:
            {
                if (!leaf.moduleName.empty())
                {
                    _frameStrings.push_back(std::string("|lm: |ns: |ct: |cg: |fn:") + leaf.moduleName + " |fg: |sg:");
                    sample->AddFrame({membreakdown::Module, _frameStrings.back(), "", 0});
                    sample->AddLabel(StringLabel{MappedFileLabel, leaf.moduleName});
                }
                sample->AddFrame({membreakdown::Module, membreakdown::MappedFiles, "", 0});
                sample->AddLabel(StringLabel{MemorySourceLabel, SourceMappedFile});
                break;
            }

            case Source::Stack:
                sample->AddFrame({membreakdown::Module, membreakdown::Stacks, "", 0});
                sample->AddLabel(StringLabel{MemorySourceLabel, SourceStack});
                sample->AddLabel(StringLabel{RegionKindLabel, SourceStack});
                break;

            case Source::Reserved:
                sample->AddFrame({membreakdown::Module, membreakdown::ReservedMem, "", 0});
                sample->AddLabel(StringLabel{MemorySourceLabel, SourceReserved});
                sample->AddLabel(StringLabel{RegionStateLabel, ToString(NativeHeapState::Reserved)});
                break;

            case Source::Private:
            default:
                sample->AddFrame({membreakdown::Module, membreakdown::PrivateMem, "", 0});
                sample->AddLabel(StringLabel{MemorySourceLabel, SourcePrivate});
                break;
        }

        // Root frame is common to every sample and must be the deepest (last) frame.
        sample->AddFrame({membreakdown::Module, membreakdown::Root, "", 0});

#ifdef LINUX
        if (leaf.rssEstimated)
        {
            sample->AddLabel(StringLabel{RssEstimatedLabel, TrueValue});
        }
#endif

        enumerator->_samples.push_back(std::move(sample));
    }

    _duration = static_cast<uint64_t>((OpSysTools::GetHighPrecisionTimestamp() - start).count() / 1000000);
    Log::Debug("MemoryBreakdownProvider: built ", enumerator->size(), " memory samples in ", _duration, " ms.");

    return enumerator;
}
