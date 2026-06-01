// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gtest/gtest.h"

#include "StackDeltaTypes.h"

TEST(StackDeltaTypesTest, UnwindInfoSizeIs12Bytes)
{
    EXPECT_EQ(sizeof(UnwindInfo), 12u);
}

TEST(StackDeltaTypesTest, CommandConstants)
{
    EXPECT_TRUE(kUnwindInfoStop.IsCommand());
    EXPECT_EQ(kUnwindInfoStop.GetCommand(), UnwindCommand::Stop);

    EXPECT_TRUE(kUnwindInfoInvalid.IsCommand());
    EXPECT_EQ(kUnwindInfoInvalid.GetCommand(), UnwindCommand::Invalid);

    EXPECT_TRUE(kUnwindInfoSignal.IsCommand());
    EXPECT_EQ(kUnwindInfoSignal.GetCommand(), UnwindCommand::Signal);

    EXPECT_TRUE(kUnwindInfoFramePointer.IsCommand());
    EXPECT_EQ(kUnwindInfoFramePointer.GetCommand(), UnwindCommand::FramePointer);
}

TEST(StackDeltaTypesTest, NonCommandIsNotCommand)
{
    UnwindInfo info;
    info.flags = UnwindFlags::None;
    info.baseReg = UnwindReg::Sp;
    info.auxBaseReg = UnwindReg::Lr;
    info.param = 16;
    info.auxParam = 0;
    info._reserved = 0;

    EXPECT_FALSE(info.IsCommand());
}

TEST(StackDeltaTypesTest, FlagsOrOperator)
{
    auto combined = UnwindFlags::Command | UnwindFlags::Frame;
    EXPECT_TRUE(combined & UnwindFlags::Command);
    EXPECT_TRUE(combined & UnwindFlags::Frame);
}

TEST(StackDeltaTypesTest, StackDeltaLayout)
{
    StackDelta sd;
    sd.address = 0x12345678;
    sd.info = kUnwindInfoStop;

    EXPECT_EQ(sd.address, 0x12345678u);
    EXPECT_TRUE(sd.info.IsCommand());
    EXPECT_EQ(sd.info.GetCommand(), UnwindCommand::Stop);
}
