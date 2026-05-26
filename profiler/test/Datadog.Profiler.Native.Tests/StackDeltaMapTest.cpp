// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gtest/gtest.h"

#include "StackDeltaMap.h"
#include "StackDeltaTypes.h"

#include <vector>

namespace {

UnwindInfo MakeSpDelta(int32_t offset)
{
    UnwindInfo info;
    info.flags = UnwindFlags::None;
    info.baseReg = UnwindReg::Sp;
    info.auxBaseReg = UnwindReg::Lr;
    info.param = offset;
    info.auxParam = 0;
    info._reserved = 0;
    return info;
}

} // namespace

TEST(StackDeltaMapTest, EmptyMapReturnsNull)
{
    StackDeltaMap map;
    map.Finalize();

    EXPECT_TRUE(map.IsEmpty());
    EXPECT_EQ(map.Lookup(0x1000), nullptr);
}

TEST(StackDeltaMapTest, SingleModuleSingleDelta)
{
    StackDeltaMap map;

    std::vector<StackDelta> deltas;
    deltas.push_back({0x1000, MakeSpDelta(16)});
    deltas.push_back({0x2000, kUnwindInfoInvalid});

    map.AddModule(0x1000, 0x2000, std::move(deltas));
    map.Finalize();

    EXPECT_EQ(map.ModuleCount(), 1u);

    // PC within the single interval
    auto* info = map.Lookup(0x1500);
    ASSERT_NE(info, nullptr);
    EXPECT_EQ(info->baseReg, UnwindReg::Sp);
    EXPECT_EQ(info->param, 16);

    // PC exactly at start
    info = map.Lookup(0x1000);
    ASSERT_NE(info, nullptr);
    EXPECT_EQ(info->param, 16);

    // PC at the invalid marker
    info = map.Lookup(0x2000);
    EXPECT_EQ(info, nullptr); // outside module range
}

TEST(StackDeltaMapTest, MultipleIntervalsInOneModule)
{
    StackDeltaMap map;

    std::vector<StackDelta> deltas;
    deltas.push_back({0x1000, MakeSpDelta(0)});    // prologue: SP+0
    deltas.push_back({0x1004, MakeSpDelta(32)});   // after push: SP+32
    deltas.push_back({0x1100, MakeSpDelta(0)});    // epilogue: SP+0
    deltas.push_back({0x1104, kUnwindInfoInvalid}); // end

    map.AddModule(0x1000, 0x2000, std::move(deltas));
    map.Finalize();

    // Before the push
    auto* info = map.Lookup(0x1000);
    ASSERT_NE(info, nullptr);
    EXPECT_EQ(info->param, 0);

    // After the push
    info = map.Lookup(0x1050);
    ASSERT_NE(info, nullptr);
    EXPECT_EQ(info->param, 32);

    // In the epilogue
    info = map.Lookup(0x1100);
    ASSERT_NE(info, nullptr);
    EXPECT_EQ(info->param, 0);
}

TEST(StackDeltaMapTest, MultipleModules)
{
    StackDeltaMap map;

    std::vector<StackDelta> deltasA;
    deltasA.push_back({0x1000, MakeSpDelta(16)});
    deltasA.push_back({0x2000, kUnwindInfoInvalid});
    map.AddModule(0x1000, 0x2000, std::move(deltasA));

    std::vector<StackDelta> deltasB;
    deltasB.push_back({0x5000, MakeSpDelta(48)});
    deltasB.push_back({0x6000, kUnwindInfoInvalid});
    map.AddModule(0x5000, 0x6000, std::move(deltasB));

    map.Finalize();

    EXPECT_EQ(map.ModuleCount(), 2u);

    auto* info = map.Lookup(0x1500);
    ASSERT_NE(info, nullptr);
    EXPECT_EQ(info->param, 16);

    info = map.Lookup(0x5500);
    ASSERT_NE(info, nullptr);
    EXPECT_EQ(info->param, 48);

    // Gap between modules
    EXPECT_EQ(map.Lookup(0x3000), nullptr);

    // Before any module
    EXPECT_EQ(map.Lookup(0x0500), nullptr);

    // After all modules
    EXPECT_EQ(map.Lookup(0x7000), nullptr);
}

TEST(StackDeltaMapTest, FramePointerCommand)
{
    StackDeltaMap map;

    std::vector<StackDelta> deltas;
    deltas.push_back({0x1000, kUnwindInfoFramePointer});
    deltas.push_back({0x2000, kUnwindInfoInvalid});

    map.AddModule(0x1000, 0x2000, std::move(deltas));
    map.Finalize();

    auto* info = map.Lookup(0x1500);
    ASSERT_NE(info, nullptr);
    EXPECT_TRUE(info->IsCommand());
    EXPECT_EQ(info->GetCommand(), UnwindCommand::FramePointer);
}

TEST(StackDeltaMapTest, StopCommand)
{
    StackDeltaMap map;

    std::vector<StackDelta> deltas;
    deltas.push_back({0x1000, kUnwindInfoStop});
    deltas.push_back({0x1010, kUnwindInfoInvalid});

    map.AddModule(0x1000, 0x2000, std::move(deltas));
    map.Finalize();

    auto* info = map.Lookup(0x1008);
    ASSERT_NE(info, nullptr);
    EXPECT_TRUE(info->IsCommand());
    EXPECT_EQ(info->GetCommand(), UnwindCommand::Stop);
}

TEST(StackDeltaMapTest, LookupBeforeFirstDeltaReturnsNull)
{
    StackDeltaMap map;

    // Module starts at 0x1000 but first delta is at 0x1100
    std::vector<StackDelta> deltas;
    deltas.push_back({0x1100, MakeSpDelta(16)});
    deltas.push_back({0x2000, kUnwindInfoInvalid});

    map.AddModule(0x1000, 0x2000, std::move(deltas));
    map.Finalize();

    // PC is in the module but before the first delta
    EXPECT_EQ(map.Lookup(0x1050), nullptr);
}
