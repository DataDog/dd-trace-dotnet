// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gtest/gtest.h"

#include "AddressRegion.h"
#include "AddressSpaceMap.h"
#include "ClrNativeHeapInfo.h"
#include "EEHeapTestHelpers.h"
#include "MemoryBreakdownProvider.h"
#include "MetricsRegistry.h"
#include "Sample.h"
#include "SampleValueTypeProvider.h"
#include "SamplesEnumerator.h"

#include <memory>
#include <string>
#include <vector>

namespace
{
constexpr size_t MemoryBreakdownIndex = 0;

AddressRegion Image(uintptr_t address, uint64_t size, const std::string& name, const std::string& protection)
{
    AddressRegion r;
    r.Address = address;
    r.Size = size;
    r.Committed = size;
    r.Category = RegionCategory::Image;
    r.ModuleName = name;
    r.Protection = protection;
    return r;
}

AddressRegion Private(uintptr_t address, uint64_t size)
{
    AddressRegion r;
    r.Address = address;
    r.Size = size;
    r.Committed = size;
    r.Category = RegionCategory::PrivateData;
    return r;
}

ClrNativeHeapInfo Segment(uintptr_t address, uint64_t size, int generation, int gcHeap)
{
    ClrNativeHeapInfo h;
    h.Address = address;
    h.Size = size;
    h.Committed = size;
    h.Kind = NativeHeapKind::GCHeapSegment;
    h.State = NativeHeapState::Active;
    h.Generation = generation;
    h.GCHeap = gcHeap;
    return h;
}

ClrNativeHeapInfo NativeHeap(uintptr_t address, uint64_t size, NativeHeapKind kind)
{
    ClrNativeHeapInfo h;
    h.Address = address;
    h.Size = size;
    h.Committed = size;
    h.Kind = kind;
    h.State = NativeHeapState::Active;
    return h;
}

std::vector<std::shared_ptr<Sample>> Collect(SamplesEnumerator* e)
{
    std::vector<std::shared_ptr<Sample>> result;
    std::shared_ptr<Sample> s;
    while (e->MoveNext(s))
    {
        result.push_back(s);
    }
    return result;
}

std::string GetStringLabel(const std::shared_ptr<Sample>& s, const std::string& key)
{
    for (auto const& l : s->GetLabels())
    {
        if (auto* sl = std::get_if<StringLabel>(&l); sl != nullptr && sl->first == key)
        {
            return sl->second;
        }
    }
    return {};
}

bool HasLabel(const std::shared_ptr<Sample>& s, const std::string& key)
{
    for (auto const& l : s->GetLabels())
    {
        if (auto* sl = std::get_if<StringLabel>(&l); sl != nullptr && sl->first == key)
        {
            return true;
        }
        if (auto* nl = std::get_if<NumericLabel>(&l); nl != nullptr && nl->first == key)
        {
            return true;
        }
    }
    return false;
}

bool FrameContains(const std::shared_ptr<Sample>& s, std::string_view needle)
{
    for (auto const& f : s->GetCallstack())
    {
        if (f.Frame.find(needle) != std::string_view::npos)
        {
            return true;
        }
    }
    return false;
}

// Index of the first frame whose name contains needle, or -1 if none.
int FrameIndex(const std::shared_ptr<Sample>& s, std::string_view needle)
{
    auto const& stack = s->GetCallstack();
    for (size_t i = 0; i < stack.size(); ++i)
    {
        if (stack[i].Frame.find(needle) != std::string_view::npos)
        {
            return static_cast<int>(i);
        }
    }
    return -1;
}

int64_t MemoryBreakdown(const std::shared_ptr<Sample>& s)
{
    return s->GetValues()[MemoryBreakdownIndex];
}

// Builds the standard scenario: a CLR module (3 protection runs) + an app module, a large private GC
// region holding gen0 (x2 heaps), gen1, gen2, LOH, POH segments and a loader heap. Committed and RSS
// are equal by default so common assertions are platform-neutral.
std::unique_ptr<AddressSpaceMap> BuildScenarioMap(bool differentRss = false)
{
    std::vector<AddressRegion> regions{
        Image(0x1000, 0x1000, "clr.dll", "r-x"),
        Image(0x2000, 0x1000, "clr.dll", "r--"),
        Image(0x3000, 0x1000, "clr.dll", "rw-"),
        Image(0x5000, 0x1000, "app.dll", "r-x"),
        Private(0x100000, 0x10000),
    };

    for (auto& region : regions)
    {
        region.Rss = differentRss ? region.Committed / 2 : region.Committed;
    }

    return std::make_unique<AddressSpaceMap>(std::move(regions), /*committed*/ true, /*rss*/ true);
}

std::vector<ClrNativeHeapInfo> BuildScenarioHeaps()
{
    return {
        Segment(0x100000, 0x1000, 0, 0),
        Segment(0x101000, 0x1000, 0, 1),
        Segment(0x102000, 0x1000, 1, 0),
        Segment(0x103000, 0x1000, 2, 0),
        Segment(0x104000, 0x1000, 3, 0), // LOH
        Segment(0x105000, 0x1000, 4, 0), // POH
        NativeHeap(0x106000, 0x1000, NativeHeapKind::HighFrequencyHeap),
    };
}
} // namespace

TEST(MemoryBreakdownProviderTest, RegistersMemoryBreakdownSampleType)
{
    auto map = BuildScenarioMap();
    FakeClrNativeHeapSnapshot snapshot(BuildScenarioHeaps(), /*available*/ true, "dac", map.get());

    SampleValueTypeProvider valueTypeProvider;
    MetricsRegistry registry;
    MemoryBreakdownProvider provider(valueTypeProvider, &snapshot, registry);

    auto const& valueTypes = valueTypeProvider.GetValueTypes();
    ASSERT_EQ(valueTypes.size(), 1u);
    EXPECT_EQ(valueTypes[MemoryBreakdownIndex].Name, "memory-breakdown");
    EXPECT_EQ(valueTypes[MemoryBreakdownIndex].Unit, "bytes");
}

TEST(MemoryBreakdownProviderTest, ReconciliationDoesNotDoubleCount)
{
    auto map = BuildScenarioMap();
    FakeClrNativeHeapSnapshot snapshot(BuildScenarioHeaps(), /*available*/ true, "dac", map.get());

    SampleValueTypeProvider valueTypeProvider;
    MetricsRegistry registry;
    MemoryBreakdownProvider provider(valueTypeProvider, &snapshot, registry);

    auto enumerator = provider.GetSamples();
    auto samples = Collect(enumerator.get());
    ASSERT_FALSE(samples.empty());

    // 4 image runs (0x4000) + private region (0x10000) = 0x14000 committed, attributed exactly once.
    int64_t total = 0;
    for (const auto& s : samples)
    {
        total += MemoryBreakdown(s);
    }
    EXPECT_EQ(total, 0x14000);
}

TEST(MemoryBreakdownProviderTest, ExportsPlatformSpecificMetric)
{
    auto map = BuildScenarioMap(/*differentRss*/ true);
    FakeClrNativeHeapSnapshot snapshot(BuildScenarioHeaps(), /*available*/ true, "dac", map.get());

    SampleValueTypeProvider valueTypeProvider;
    MetricsRegistry registry;
    MemoryBreakdownProvider provider(valueTypeProvider, &snapshot, registry);

    auto samples = Collect(provider.GetSamples().get());
    ASSERT_FALSE(samples.empty());

    int64_t total = 0;
    for (const auto& sample : samples)
    {
        total += MemoryBreakdown(sample);
    }

#ifdef _WINDOWS
    EXPECT_EQ(total, 0x14000);
#elif defined(LINUX)
    EXPECT_EQ(total, 0xa000);
#else
#error Unsupported platform
#endif
}

TEST(MemoryBreakdownProviderTest, SameGenerationAcrossHeapsCollapsesIntoOneLeaf)
{
    auto map = BuildScenarioMap();
    FakeClrNativeHeapSnapshot snapshot(BuildScenarioHeaps(), /*available*/ true, "dac", map.get());

    SampleValueTypeProvider valueTypeProvider;
    MetricsRegistry registry;
    MemoryBreakdownProvider provider(valueTypeProvider, &snapshot, registry);

    auto enumerator = provider.GetSamples();
    auto samples = Collect(enumerator.get());

    // The two gen0 segments (heap 0 and heap 1) must collapse into a single gen0 sample summing 0x2000.
    int gen0Count = 0;
    int64_t gen0Memory = 0;
    for (const auto& s : samples)
    {
        if (FrameContains(s, "fn:gen0 "))
        {
            gen0Count++;
            gen0Memory += MemoryBreakdown(s);
            EXPECT_EQ(GetStringLabel(s, Sample::GarbageCollectionGenerationLabel), "0");
        }
    }
    EXPECT_EQ(gen0Count, 1);
    EXPECT_EQ(gen0Memory, 0x2000);
}

TEST(MemoryBreakdownProviderTest, ManagedGenerationFramesAndLabels)
{
    auto map = BuildScenarioMap();
    FakeClrNativeHeapSnapshot snapshot(BuildScenarioHeaps(), /*available*/ true, "dac", map.get());

    SampleValueTypeProvider valueTypeProvider;
    MetricsRegistry registry;
    MemoryBreakdownProvider provider(valueTypeProvider, &snapshot, registry);

    auto samples = Collect(provider.GetSamples().get());

    struct Expect
    {
        std::string_view frame;
        const char* generation;
    };
    const Expect expected[] = {
        {"fn:gen1 ", "1"},
        {"fn:gen2 ", "2"},
        {"fn:LOH ", "3"},
        {"fn:POH ", "4"},
    };

    for (const auto& e : expected)
    {
        bool found = false;
        for (const auto& s : samples)
        {
            if (FrameContains(s, e.frame))
            {
                found = true;
                EXPECT_EQ(GetStringLabel(s, Sample::GarbageCollectionGenerationLabel), e.generation);
                EXPECT_EQ(GetStringLabel(s, "memory_source"), "managed");
                EXPECT_TRUE(FrameContains(s, "fn:Managed Heap (GC) "));
                EXPECT_TRUE(FrameContains(s, "fn:Process Memory "));
            }
        }
        EXPECT_TRUE(found) << "missing frame " << e.frame;
    }
}

TEST(MemoryBreakdownProviderTest, FramesAreEmittedLeafFirst)
{
    // pprof/libdatadog convention: locations[0] is the leaf and the last location is the root.
    // Every memory sample must therefore end with the "Process Memory" root frame, and a managed
    // generation sample must be ordered leaf (gen) -> group (Managed Heap) -> root (Process Memory).
    auto map = BuildScenarioMap();
    FakeClrNativeHeapSnapshot snapshot(BuildScenarioHeaps(), /*available*/ true, "dac", map.get());

    SampleValueTypeProvider valueTypeProvider;
    MetricsRegistry registry;
    MemoryBreakdownProvider provider(valueTypeProvider, &snapshot, registry);

    auto samples = Collect(provider.GetSamples().get());
    ASSERT_FALSE(samples.empty());

    // The root frame is common to every sample and must be the deepest (last) frame.
    for (const auto& s : samples)
    {
        auto const& stack = s->GetCallstack();
        ASSERT_FALSE(stack.empty());
        EXPECT_NE(stack.back().Frame.find("fn:Process Memory "), std::string_view::npos)
            << "last frame must be the Process Memory root";
    }

    // Find the gen0 sample and assert its leaf -> group -> root ordering.
    bool checkedGen0 = false;
    for (const auto& s : samples)
    {
        const int gen0 = FrameIndex(s, "fn:gen0 ");
        if (gen0 < 0)
        {
            continue;
        }
        checkedGen0 = true;

        const int group = FrameIndex(s, "fn:Managed Heap (GC) ");
        const int root = FrameIndex(s, "fn:Process Memory ");

        ASSERT_GE(group, 0);
        ASSERT_GE(root, 0);
        EXPECT_LT(gen0, group) << "leaf (gen0) must come before its group";
        EXPECT_LT(group, root) << "group must come before the root";
        EXPECT_EQ(gen0, 0) << "leaf must be at index 0";
    }
    EXPECT_TRUE(checkedGen0) << "no gen0 sample to validate ordering";
}

TEST(MemoryBreakdownProviderTest, ClrNativeLeafHasGroupAndKindLabels)
{
    auto map = BuildScenarioMap();
    FakeClrNativeHeapSnapshot snapshot(BuildScenarioHeaps(), /*available*/ true, "dac", map.get());

    SampleValueTypeProvider valueTypeProvider;
    MetricsRegistry registry;
    MemoryBreakdownProvider provider(valueTypeProvider, &snapshot, registry);

    auto samples = Collect(provider.GetSamples().get());

    bool found = false;
    for (const auto& s : samples)
    {
        if (GetStringLabel(s, "region_kind") == "HighFrequencyHeap")
        {
            found = true;
            EXPECT_EQ(GetStringLabel(s, "memory_source"), "clr-native");
            EXPECT_EQ(GetStringLabel(s, "region_group"), "Loader");
            EXPECT_TRUE(FrameContains(s, "fn:Loader "));
            EXPECT_TRUE(FrameContains(s, "fn:CLR Native "));
        }
    }
    EXPECT_TRUE(found);
}

TEST(MemoryBreakdownProviderTest, ModuleProtectionRunsCollapseIntoOneSample)
{
    auto map = BuildScenarioMap();
    FakeClrNativeHeapSnapshot snapshot(BuildScenarioHeaps(), /*available*/ true, "dac", map.get());

    SampleValueTypeProvider valueTypeProvider;
    MetricsRegistry registry;
    MemoryBreakdownProvider provider(valueTypeProvider, &snapshot, registry);

    auto samples = Collect(provider.GetSamples().get());

    int clrModuleCount = 0;
    int64_t clrModuleMemory = 0;
    for (const auto& s : samples)
    {
        if (GetStringLabel(s, "module") == "clr.dll")
        {
            clrModuleCount++;
            clrModuleMemory += MemoryBreakdown(s);
            EXPECT_EQ(GetStringLabel(s, "memory_source"), "image");
            // protection must never be surfaced as a sample label (would re-fragment the module).
            EXPECT_FALSE(HasLabel(s, "protection"));
        }
    }

    // The 3 protection runs (r-x/r--/rw-) collapse into exactly one clr.dll sample of 0x3000.
    EXPECT_EQ(clrModuleCount, 1);
    EXPECT_EQ(clrModuleMemory, 0x3000);
}

TEST(MemoryBreakdownProviderTest, PrivateRemainderIsAttributedToPrivateLeaf)
{
    auto map = BuildScenarioMap();
    FakeClrNativeHeapSnapshot snapshot(BuildScenarioHeaps(), /*available*/ true, "dac", map.get());

    SampleValueTypeProvider valueTypeProvider;
    MetricsRegistry registry;
    MemoryBreakdownProvider provider(valueTypeProvider, &snapshot, registry);

    auto samples = Collect(provider.GetSamples().get());

    // Private region 0x10000 minus 7 CLR sub-regions (0x7000) => 0x9000 attributed to the private leaf.
    int64_t privateMemory = 0;
    for (const auto& s : samples)
    {
        if (GetStringLabel(s, "memory_source") == "private")
        {
            privateMemory += MemoryBreakdown(s);
            EXPECT_TRUE(FrameContains(s, "fn:Native Heap / Private "));
        }
    }
    EXPECT_EQ(privateMemory, 0x9000);
}

TEST(MemoryBreakdownProviderTest, EverySampleHasMemorySourceWithoutAppDomainLabels)
{
    auto map = BuildScenarioMap();
    FakeClrNativeHeapSnapshot snapshot(BuildScenarioHeaps(), /*available*/ true, "dac", map.get());

    SampleValueTypeProvider valueTypeProvider;
    MetricsRegistry registry;
    MemoryBreakdownProvider provider(valueTypeProvider, &snapshot, registry);

    auto samples = Collect(provider.GetSamples().get());
    ASSERT_FALSE(samples.empty());

    for (const auto& s : samples)
    {
        EXPECT_FALSE(GetStringLabel(s, "memory_source").empty());
        EXPECT_FALSE(HasLabel(s, Sample::ProcessIdLabel));
        EXPECT_FALSE(HasLabel(s, Sample::AppDomainNameLabel));
    }
}

TEST(MemoryBreakdownProviderTest, NoSamplesWhenSnapshotUnavailable)
{
    FakeClrNativeHeapSnapshot snapshot(BuildScenarioHeaps(), /*available*/ false, "dac", nullptr);

    SampleValueTypeProvider valueTypeProvider;
    MetricsRegistry registry;
    MemoryBreakdownProvider provider(valueTypeProvider, &snapshot, registry);

    auto enumerator = provider.GetSamples();
    EXPECT_EQ(enumerator->size(), 0u);
}

TEST(MemoryBreakdownProviderTest, RegistersDurationMetrics)
{
    auto map = BuildScenarioMap();
    FakeClrNativeHeapSnapshot snapshot(BuildScenarioHeaps(), /*available*/ true, "dac", map.get());

    SampleValueTypeProvider valueTypeProvider;
    MetricsRegistry registry;
    MemoryBreakdownProvider provider(valueTypeProvider, &snapshot, registry);

    provider.GetSamples();

    bool foundDuration = false;
    size_t metricCount = 0;
    for (const auto& [name, value] : registry.Collect())
    {
        metricCount++;
        if (name == "dotnet_memory_breakdown_duration")
        {
            foundDuration = true;
        }
    }
    EXPECT_TRUE(foundDuration);
    EXPECT_EQ(metricCount, 1u);
}
