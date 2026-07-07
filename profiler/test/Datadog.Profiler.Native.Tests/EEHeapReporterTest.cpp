// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gtest/gtest.h"

#include "ClrNativeHeapInfo.h"
#include "EEHeapReporter.h"
#include "EEHeapTestHelpers.h"
#include "MetricsRegistry.h"
#include "RuntimeInfoHelper.h"

#include <memory>
#include <string>
#include <vector>

namespace
{
std::vector<ClrNativeHeapInfo> SampleHeaps()
{
    std::vector<ClrNativeHeapInfo> heaps;

    ClrNativeHeapInfo code;
    code.Address = 0x1000;
    code.Size = 65536;
    code.Committed = 32768;
    code.Kind = NativeHeapKind::LoaderCodeHeap;
    code.State = NativeHeapState::Active;
    heaps.push_back(code);

    ClrNativeHeapInfo gc;
    gc.Address = 0x20000;
    gc.Size = 4096;
    gc.Committed = 2048;
    gc.Kind = NativeHeapKind::GCHeapSegment;
    gc.State = NativeHeapState::Active;
    gc.GCHeap = 2;
    gc.Generation = 2;
    heaps.push_back(gc);

    return heaps;
}
} // namespace

// --- backend selection --------------------------------------------------------------------------

TEST(EEHeapReporterTest, ShouldUseCdacForNet11AndAbove)
{
    RuntimeInfoHelper net11(11, 0, false);
    EXPECT_TRUE(EEHeapReporter::ShouldUseCdac(net11.GetRuntimeInfo()));

    RuntimeInfoHelper net12(12, 0, false);
    EXPECT_TRUE(EEHeapReporter::ShouldUseCdac(net12.GetRuntimeInfo()));
}

TEST(EEHeapReporterTest, ShouldUseDacForPreNet11)
{
    RuntimeInfoHelper net6(6, 0, false);
    RuntimeInfoHelper net8(8, 0, false);
    RuntimeInfoHelper net10(10, 0, false);

    EXPECT_FALSE(EEHeapReporter::ShouldUseCdac(net6.GetRuntimeInfo()));
    EXPECT_FALSE(EEHeapReporter::ShouldUseCdac(net8.GetRuntimeInfo()));
    EXPECT_FALSE(EEHeapReporter::ShouldUseCdac(net10.GetRuntimeInfo()));
}

TEST(EEHeapReporterTest, ShouldUseDacForFrameworkEvenWhenMajorIsHigh)
{
    RuntimeInfoHelper framework(11, 0, true);
    EXPECT_FALSE(EEHeapReporter::ShouldUseCdac(framework.GetRuntimeInfo()));
}

TEST(EEHeapReporterTest, ShouldUseDacWhenRuntimeInfoIsNull)
{
    EXPECT_FALSE(EEHeapReporter::ShouldUseCdac(nullptr));
}

// --- JSON serialization -------------------------------------------------------------------------

TEST(EEHeapReporterTest, ToJsonProducesExpectedShape)
{
    std::string json = EEHeapReporter::ToJson("cdac", SampleHeaps());

    EXPECT_NE(json.find("\"source\":\"cdac\""), std::string::npos);
    EXPECT_NE(json.find("\"address\":\"0x1000\""), std::string::npos);
    EXPECT_NE(json.find("\"size\":65536"), std::string::npos);
    EXPECT_NE(json.find("\"kind\":\"LoaderCodeHeap\""), std::string::npos);
    EXPECT_NE(json.find("\"group\":\"Code\""), std::string::npos);
    EXPECT_NE(json.find("\"state\":\"Active\""), std::string::npos);

    // committed is always emitted (for both records).
    EXPECT_NE(json.find("\"committed\":32768"), std::string::npos);
    EXPECT_NE(json.find("\"committed\":2048"), std::string::npos);

    // GC-heap-specific records carry the gc_heap index + generation; non-GC records omit them, and
    // the sentinel -1 is never serialized.
    EXPECT_NE(json.find("\"kind\":\"GCHeapSegment\""), std::string::npos);
    EXPECT_NE(json.find("\"group\":\"GC Object Heap\""), std::string::npos);
    EXPECT_NE(json.find("\"gc_heap\":2"), std::string::npos);
    EXPECT_NE(json.find("\"generation\":2"), std::string::npos);
    EXPECT_EQ(json.find("\"generation\":-1"), std::string::npos);
}

// --- enumeration + metric (via an injected fake backend) ----------------------------------------

TEST(EEHeapReporterTest, GetContentEnumeratesAndRecordsDurationMetric)
{
    MetricsRegistry registry;
    RuntimeInfoHelper net8(8, 0, false);
    EEHeapReporter reporter(nullptr, net8.GetRuntimeInfo(), registry);

    auto fake = std::make_unique<FakeNativeHeapEnumerator>(SampleHeaps(), /*available*/ true);
    auto* fakePtr = fake.get();
    reporter.InjectEnumeratorForTest(std::move(fake), "dac");

    std::string content = reporter.GetAndClearEEHeapContent();
    EXPECT_FALSE(content.empty());
    EXPECT_NE(content.find("\"source\":\"dac\""), std::string::npos);
    EXPECT_EQ(fakePtr->EnumerateCount(), 1);

    // The dotnet_eeheap_duration ProxyMetric must be registered.
    auto metrics = registry.Collect();
    bool found = false;
    for (const auto& [name, value] : metrics)
    {
        if (name == "dotnet_eeheap_duration")
        {
            found = true;
        }
    }
    EXPECT_TRUE(found);
}

TEST(EEHeapReporterTest, GetContentReturnsEmptyWhenBackendYieldsNoHeaps)
{
    MetricsRegistry registry;
    RuntimeInfoHelper net8(8, 0, false);
    EEHeapReporter reporter(nullptr, net8.GetRuntimeInfo(), registry);

    auto fake = std::make_unique<FakeNativeHeapEnumerator>(std::vector<ClrNativeHeapInfo>{}, /*available*/ true);
    reporter.InjectEnumeratorForTest(std::move(fake), "dac");

    EXPECT_TRUE(reporter.GetAndClearEEHeapContent().empty());
}

// --- ServiceBase lifecycle ----------------------------------------------------------------------

TEST(EEHeapReporterTest, ServiceLifecycleStartsAndStopsOnce)
{
    MetricsRegistry registry;
    RuntimeInfoHelper net8(8, 0, false);
    EEHeapReporter reporter(nullptr, net8.GetRuntimeInfo(), registry);

    EXPECT_TRUE(reporter.Start());
    EXPECT_FALSE(reporter.Start());
    EXPECT_TRUE(reporter.Stop());
    EXPECT_FALSE(reporter.Stop());
}
