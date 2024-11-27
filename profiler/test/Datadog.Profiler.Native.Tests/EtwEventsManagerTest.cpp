// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
#ifdef _WINDOWS

#include "gmock/gmock.h"
#include "gtest/gtest.h"

#include "ClrEventsParser.h"
#include "FrameStore.h"
#include "IAllocationsListener.h"
#include "IContentionListener.h"

#include "ProfilerMockedInterface.h"

#include "profiler/src/ProfilerEngine/Datadog.Profiler.Native.Windows/EtwEventsManager.h"

#include <chrono>

using ::testing::_;
using ::testing::ElementsAre;
using ::testing::IsEmpty;
using ::testing::Return;
using ::testing::ReturnRef;

using namespace std::chrono_literals;

TEST(EtwEventsManagerTest, ContentionEventWithoutCallstack)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();

    EXPECT_CALL(mockConfiguration, IsEtwLoggingEnabled()).WillRepeatedly(Return(false));
    std::string replayEndpoint = "not empty to simulate replay";
    EXPECT_CALL(mockConfiguration, GetEtwReplayEndpoint()).WillRepeatedly(ReturnRef(replayEndpoint));

    auto [contentionListener, mockContentionListener] = CreateMockForUniquePtr<IContentionListener, MockContentionListener>();
    //                                                 epoch timestamp    , tid, duration, stack
    EXPECT_CALL(mockContentionListener, OnContention(1601910785587000000ns, 2  , 14300ns , IsEmpty()));
    auto manager = EtwEventsManager(nullptr, contentionListener.get(), nullptr, configuration.get());

    manager.OnEvent(etw_timestamp(132463843855875757), 2, 1, KEYWORD_CONTENTION, 1, EVENT_CONTENTION_START, 0, nullptr);
    manager.OnEvent(etw_timestamp(132463843855875900), 2, 1, KEYWORD_CONTENTION, 1, EVENT_CONTENTION_STOP, 0, nullptr);
}

TEST(EtwEventsManagerTest, ContentionEventWithCallstack)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();

    EXPECT_CALL(mockConfiguration, IsEtwLoggingEnabled()).WillRepeatedly(Return(false));
    std::string replayEndpoint = "not empty to simulate replay";
    EXPECT_CALL(mockConfiguration, GetEtwReplayEndpoint()).WillRepeatedly(ReturnRef(replayEndpoint));

    auto [contentionListener, mockContentionListener] = CreateMockForUniquePtr<IContentionListener, MockContentionListener>();

    //                                               epoch timestamp      , tid, duration, stack
    EXPECT_CALL(mockContentionListener, OnContention(1601910785587000000ns, 2  , 14300ns , ElementsAre(FrameStore::FakeLockContentionIP)));
    auto manager = EtwEventsManager(nullptr, contentionListener.get(), nullptr, configuration.get());

    manager.OnEvent(etw_timestamp(132463843855875757), 2, 1, KEYWORD_CONTENTION, 1, EVENT_CONTENTION_START, 0, nullptr);
    manager.OnEvent(etw_timestamp(132463843855875800), 2, 1, KEYWORD_STACKWALK, 1, -1, 0, nullptr);
    manager.OnEvent(etw_timestamp(132463843855875900), 2, 1, KEYWORD_CONTENTION, 1, EVENT_CONTENTION_STOP, 0, nullptr);
}

TEST(EtwEventsManagerTest, AllocationTickEventWithCallstack)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();

    EXPECT_CALL(mockConfiguration, IsEtwLoggingEnabled()).WillRepeatedly(Return(false));
    std::string replayEndpoint = "not empty to simulate replay";
    EXPECT_CALL(mockConfiguration, GetEtwReplayEndpoint()).WillRepeatedly(ReturnRef(replayEndpoint));

    auto [allocationListener, mockAllocationListener] = CreateMockForUniquePtr<IAllocationsListener, MockAllocationListener>();

    auto manager = EtwEventsManager(allocationListener.get(), nullptr, nullptr, configuration.get());

    constexpr auto typeName = WStr("MyType");
    constexpr auto typeNbBytes = std::char_traits<WCHAR>::length(typeName) * sizeof(WCHAR);
    std::uint8_t buffer[sizeof(AllocationTickV3Payload) + typeNbBytes + 1] = {0};

    auto* payload = (AllocationTickV3Payload*)&buffer;

    payload->AllocationAmount = 21;
    payload->AllocationKind = 0x0;
    payload->ClrInstanceId = 1;
    payload->AllocationAmount64 = 21;
    memcpy(&payload->FirstCharInName, typeName, typeNbBytes);
    (&payload->FirstCharInName)[typeNbBytes] = WStr('\0');

    EXPECT_CALL(mockAllocationListener,
                OnAllocation(
                    1601910785587000000ns,
                    2,
                    payload->AllocationKind,
                    0, // replay mode ClassID is 0
                    "MyType",
                    payload->AllocationAmount64,
                    ElementsAre(FrameStore::FakeAllocationIP)));

    manager.OnEvent(etw_timestamp(132463843855875757), 2, 1, KEYWORD_GC, 1, EVENT_ALLOCATION_TICK, 0, reinterpret_cast<const uint8_t*>(&buffer));
    manager.OnEvent(etw_timestamp(132463843855875800), 2, 1, KEYWORD_STACKWALK, 1, -1, 0, nullptr);
}

TEST(EtwEventsManagerTest, AllocationTickEventWithoutCallstack)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();

    EXPECT_CALL(mockConfiguration, IsEtwLoggingEnabled()).WillRepeatedly(Return(false));
    std::string replayEndpoint = "not empty to simulate replay";
    EXPECT_CALL(mockConfiguration, GetEtwReplayEndpoint()).WillRepeatedly(ReturnRef(replayEndpoint));

    auto [allocationListener, mockAllocationListener] = CreateMockForUniquePtr<IAllocationsListener, MockAllocationListener>();

    auto manager = EtwEventsManager(allocationListener.get(), nullptr, nullptr, configuration.get());

    constexpr auto typeName = WStr("MyType");
    constexpr auto typeNbBytes = std::char_traits<WCHAR>::length(typeName) * sizeof(WCHAR);
    std::uint8_t buffer[sizeof(AllocationTickV3Payload) + typeNbBytes + 1] = {0};

    auto* payload = reinterpret_cast<AllocationTickV3Payload*>(&buffer);

    payload->AllocationAmount = 21;
    payload->AllocationKind = 0x0;
    payload->ClrInstanceId = 1;
    payload->AllocationAmount64 = 21;
    memcpy(&payload->FirstCharInName, typeName, typeNbBytes);
    (&payload->FirstCharInName)[typeNbBytes] = WStr('\0');

    EXPECT_CALL(mockAllocationListener,
                OnAllocation(
                    1601910785587000000ns,
                    2,
                    payload->AllocationKind,
                    0, // replay mode ClassID is 0
                    "MyType",
                    payload->AllocationAmount64,
                    ElementsAre(FrameStore::FakeAllocationIP)))
        .Times(0);

    manager.OnEvent(etw_timestamp(132463843855875757), 2, 1, KEYWORD_GC, 1, EVENT_ALLOCATION_TICK, 0, reinterpret_cast<const uint8_t*>(&buffer));
}
#endif