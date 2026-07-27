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

#include <array>
#include <chrono>
#include <cstddef>
#include <cstring>
#include <memory>
#include <string>
#include <thread>
#include <vector>

using ::testing::_;
using ::testing::ElementsAre;
using ::testing::IsEmpty;
using ::testing::Return;
using ::testing::ReturnRef;

using namespace std::chrono_literals;

namespace
{
constexpr uint32_t AllocationTickTypeNameOffset = offsetof(AllocationTickV3Payload, FirstCharInName);

struct TestLogger : public IIpcLogger
{
    void Info(std::string line) const override
    {
    }

    void Warn(std::string line) const override
    {
    }

    void Error(std::string line) const override
    {
    }
};

class TestEtwEventsReceiver : public IEtwEventsReceiver
{
public:
    uint32_t EventCount = 0;
    uint32_t LastEventDataSize = 0;

    void OnEvent(
        etw_timestamp timestamp,
        uint32_t tid,
        uint32_t version,
        uint64_t keyword,
        uint8_t level,
        uint32_t id,
        uint32_t cbEventData,
        const uint8_t* pEventData) override
    {
        EventCount++;
        LastEventDataSize = cbEventData;
    }

    void OnStop() override
    {
    }
};

std::vector<uint8_t> CreateAllocationTickPayload(const WCHAR* typeName, bool includeNullTerminator)
{
    const size_t typeNameLength = std::char_traits<WCHAR>::length(typeName);
    const size_t typeNameCharCount = typeNameLength + (includeNullTerminator ? 1 : 0);
    std::vector<uint8_t> buffer(AllocationTickTypeNameOffset + (typeNameCharCount * sizeof(WCHAR)));
    auto* payload = reinterpret_cast<AllocationTickV3Payload*>(buffer.data());

    payload->AllocationAmount = 21;
    payload->AllocationKind = 0x0;
    payload->ClrInstanceId = 1;
    payload->AllocationAmount64 = 21;
    memcpy(&payload->FirstCharInName, typeName, typeNameLength * sizeof(WCHAR));
    if (includeNullTerminator)
    {
        (&payload->FirstCharInName)[typeNameLength] = WStr('\0');
    }

    return buffer;
}

std::vector<uint8_t> CreateStackWalkPayload(uint32_t frameCount, std::initializer_list<uintptr_t> frames)
{
    const size_t frameStorageSize = (frames.size() == 0) ? 0 : (frames.size() - 1) * sizeof(uintptr_t);
    std::vector<uint8_t> buffer(sizeof(StackWalkPayload) + frameStorageSize);
    auto* payload = reinterpret_cast<StackWalkPayload*>(buffer.data());
    payload->FrameCount = frameCount;

    size_t index = 0;
    for (auto frame : frames)
    {
        payload->Stack[index++] = frame;
    }

    return buffer;
}

std::vector<uint8_t> CreateClrEventsMessage(uint16_t declaredPayloadSize, size_t actualPayloadSize)
{
    const size_t clrEventsHeaderSize = sizeof(IpcHeader) + sizeof(EVENT_HEADER) + sizeof(uint16_t);
    std::vector<uint8_t> buffer(clrEventsHeaderSize + actualPayloadSize);
    auto* message = reinterpret_cast<ClrEventsMessage*>(buffer.data());

    memcpy(message->Magic, &DD_Ipc_Magic_V1, sizeof(DD_Ipc_Magic_V1));
    message->Size = static_cast<uint16_t>(buffer.size());
    message->CommandId = Commands::ClrEvents;
    message->EtwHeader.EventDescriptor.Keyword = KEYWORD_GC;
    message->EtwHeader.EventDescriptor.Id = EVENT_ALLOCATION_TICK;
    message->Payload.EtwUserDataLength = declaredPayloadSize;

    return buffer;
}

void SendPipeMessage(const void* message, DWORD messageSize, TestEtwEventsReceiver& receiver)
{
    constexpr auto PipeTimeoutMs = 5000;
    std::string pipeName = "\\\\.\\pipe\\DD_ETW_TEST_";
    pipeName += std::to_string(::GetCurrentProcessId());
    pipeName += "_";
    pipeName += std::to_string(::GetTickCount64());

    HANDLE serverPipe = ::CreateNamedPipeA(
        pipeName.c_str(),
        PIPE_ACCESS_DUPLEX,
        PIPE_TYPE_MESSAGE | PIPE_READMODE_MESSAGE | PIPE_WAIT | PIPE_REJECT_REMOTE_CLIENTS,
        1,
        sizeof(SuccessResponse),
        (1 << 16) + sizeof(IpcHeader),
        PipeTimeoutMs,
        nullptr);
    ASSERT_NE(INVALID_HANDLE_VALUE, serverPipe);

    // Connect the client and write the message *before* starting the reader thread. This makes the
    // test deterministic: the server-side ConnectNamedPipe() runs while the client handle is still
    // open (so it reports the connection instead of racing into ERROR_NO_DATA when a client writes
    // and disconnects too quickly), and the bytes are buffered in the pipe so they remain readable
    // after the client handle is closed. Doing all the fatal ASSERT_* checks here, before the thread
    // exists, also guarantees we never return while a joinable std::thread would call std::terminate().
    HANDLE clientPipe = ::CreateFileA(
        pipeName.c_str(),
        GENERIC_READ | GENERIC_WRITE,
        0,
        nullptr,
        OPEN_EXISTING,
        SECURITY_SQOS_PRESENT | SECURITY_ANONYMOUS | FILE_FLAG_NO_BUFFERING | FILE_FLAG_WRITE_THROUGH,
        nullptr);
    ASSERT_NE(INVALID_HANDLE_VALUE, clientPipe);

    const bool connected = ::ConnectNamedPipe(serverPipe, nullptr) || (::GetLastError() == ERROR_PIPE_CONNECTED);

    DWORD bytesWritten = 0;
    const bool writeSucceeded = ::WriteFile(clientPipe, message, messageSize, &bytesWritten, nullptr) != FALSE;

    auto logger = std::make_shared<TestLogger>();
    EtwEventsHandler handler(logger, &receiver, nullptr);
    auto serverThread = std::thread([&handler, serverPipe, connected]() {
        if (connected)
        {
            handler.OnConnect(serverPipe);
        }
    });

    // Closing the client end makes the reader loop see a broken pipe once the buffered message has
    // been consumed, which is what lets OnConnect() return so the thread can be joined.
    ::CloseHandle(clientPipe);
    serverThread.join();
    ::CloseHandle(serverPipe);

    EXPECT_TRUE(connected);
    EXPECT_TRUE(writeSucceeded);
    EXPECT_EQ(messageSize, bytesWritten);
}

}

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

    auto buffer = CreateAllocationTickPayload(WStr("MyType"), true);
    auto* payload = reinterpret_cast<AllocationTickV3Payload*>(buffer.data());

    EXPECT_CALL(mockAllocationListener,
                OnAllocation(
                    1601910785587000000ns,
                    2,
                    payload->AllocationKind,
                    0, // replay mode ClassID is 0
                    "MyType",
                    payload->AllocationAmount64,
                    ElementsAre(FrameStore::FakeAllocationIP)));

    manager.OnEvent(etw_timestamp(132463843855875757), 2, 1, KEYWORD_GC, 1, EVENT_ALLOCATION_TICK, static_cast<uint32_t>(buffer.size()), buffer.data());
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

    auto buffer = CreateAllocationTickPayload(WStr("MyType"), true);
    auto* payload = reinterpret_cast<AllocationTickV3Payload*>(buffer.data());

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

    manager.OnEvent(etw_timestamp(132463843855875757), 2, 1, KEYWORD_GC, 1, EVENT_ALLOCATION_TICK, static_cast<uint32_t>(buffer.size()), buffer.data());
}

TEST(EtwEventsManagerTest, AllocationTickEventWithUnterminatedTypeNameIsIgnored)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();

    EXPECT_CALL(mockConfiguration, IsEtwLoggingEnabled()).WillRepeatedly(Return(false));
    std::string replayEndpoint = "not empty to simulate replay";
    EXPECT_CALL(mockConfiguration, GetEtwReplayEndpoint()).WillRepeatedly(ReturnRef(replayEndpoint));

    auto [allocationListener, mockAllocationListener] = CreateMockForUniquePtr<IAllocationsListener, MockAllocationListener>();
    auto manager = EtwEventsManager(allocationListener.get(), nullptr, nullptr, configuration.get());

    auto buffer = CreateAllocationTickPayload(WStr("MyType"), false);

    EXPECT_CALL(mockAllocationListener, OnAllocation(_, _, _, _, _, _, _)).Times(0);

    manager.OnEvent(etw_timestamp(132463843855875757), 2, 1, KEYWORD_GC, 1, EVENT_ALLOCATION_TICK, static_cast<uint32_t>(buffer.size()), buffer.data());
    manager.OnEvent(etw_timestamp(132463843855875800), 2, 1, KEYWORD_STACKWALK, 1, -1, 0, nullptr);
}

TEST(EtwEventsManagerTest, AllocationTickEventWithTruncatedFixedFieldsIsIgnored)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();

    EXPECT_CALL(mockConfiguration, IsEtwLoggingEnabled()).WillRepeatedly(Return(false));
    std::string replayEndpoint = "not empty to simulate replay";
    EXPECT_CALL(mockConfiguration, GetEtwReplayEndpoint()).WillRepeatedly(ReturnRef(replayEndpoint));

    auto [allocationListener, mockAllocationListener] = CreateMockForUniquePtr<IAllocationsListener, MockAllocationListener>();
    auto manager = EtwEventsManager(allocationListener.get(), nullptr, nullptr, configuration.get());

    std::vector<uint8_t> buffer(AllocationTickTypeNameOffset - 1);

    EXPECT_CALL(mockAllocationListener, OnAllocation(_, _, _, _, _, _, _)).Times(0);

    manager.OnEvent(etw_timestamp(132463843855875757), 2, 1, KEYWORD_GC, 1, EVENT_ALLOCATION_TICK, static_cast<uint32_t>(buffer.size()), buffer.data());
    manager.OnEvent(etw_timestamp(132463843855875800), 2, 1, KEYWORD_STACKWALK, 1, -1, 0, nullptr);
}

TEST(EtwEventsManagerTest, StackWalkWithOversizedFrameCountIsIgnored)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();

    EXPECT_CALL(mockConfiguration, IsEtwLoggingEnabled()).WillRepeatedly(Return(false));
    std::string replayEndpoint;
    EXPECT_CALL(mockConfiguration, GetEtwReplayEndpoint()).WillRepeatedly(ReturnRef(replayEndpoint));

    auto [allocationListener, mockAllocationListener] = CreateMockForUniquePtr<IAllocationsListener, MockAllocationListener>();
    auto manager = EtwEventsManager(allocationListener.get(), nullptr, nullptr, configuration.get());

    auto allocationPayload = CreateAllocationTickPayload(WStr("MyType"), true);
    auto stackWalkPayload = CreateStackWalkPayload(UINT32_MAX, {0x1234});

    EXPECT_CALL(mockAllocationListener,
                OnAllocation(
                    _,
                    _,
                    _,
                    _,
                    "MyType",
                    _,
                    IsEmpty()));

    manager.OnEvent(etw_timestamp(132463843855875757), 2, 1, KEYWORD_GC, 1, EVENT_ALLOCATION_TICK, static_cast<uint32_t>(allocationPayload.size()), allocationPayload.data());
    manager.OnEvent(etw_timestamp(132463843855875800), 2, 1, KEYWORD_STACKWALK, 1, -1, static_cast<uint32_t>(stackWalkPayload.size()), stackWalkPayload.data());
}

TEST(EtwEventsManagerTest, StackWalkWithFrameCountAtLimitIsAttached)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();

    EXPECT_CALL(mockConfiguration, IsEtwLoggingEnabled()).WillRepeatedly(Return(false));
    // empty replay endpoint so the real frames are parsed by AttachCallstack (the bounds check under test)
    std::string replayEndpoint;
    EXPECT_CALL(mockConfiguration, GetEtwReplayEndpoint()).WillRepeatedly(ReturnRef(replayEndpoint));

    auto [allocationListener, mockAllocationListener] = CreateMockForUniquePtr<IAllocationsListener, MockAllocationListener>();
    auto manager = EtwEventsManager(allocationListener.get(), nullptr, nullptr, configuration.get());

    auto allocationPayload = CreateAllocationTickPayload(WStr("MyType"), true);
    // the buffer holds exactly 3 frames, so maxFrames == 3: FrameCount == maxFrames must be accepted
    auto stackWalkPayload = CreateStackWalkPayload(3, {0x1111, 0x2222, 0x3333});

    EXPECT_CALL(mockAllocationListener,
                OnAllocation(
                    _,
                    _,
                    _,
                    _,
                    "MyType",
                    _,
                    ElementsAre(
                        static_cast<uintptr_t>(0x1111),
                        static_cast<uintptr_t>(0x2222),
                        static_cast<uintptr_t>(0x3333))));

    manager.OnEvent(etw_timestamp(132463843855875757), 2, 1, KEYWORD_GC, 1, EVENT_ALLOCATION_TICK, static_cast<uint32_t>(allocationPayload.size()), allocationPayload.data());
    manager.OnEvent(etw_timestamp(132463843855875800), 2, 1, KEYWORD_STACKWALK, 1, -1, static_cast<uint32_t>(stackWalkPayload.size()), stackWalkPayload.data());
}

TEST(EtwEventsManagerTest, StackWalkWithFrameCountAboveLimitIsIgnored)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();

    EXPECT_CALL(mockConfiguration, IsEtwLoggingEnabled()).WillRepeatedly(Return(false));
    std::string replayEndpoint;
    EXPECT_CALL(mockConfiguration, GetEtwReplayEndpoint()).WillRepeatedly(ReturnRef(replayEndpoint));

    auto [allocationListener, mockAllocationListener] = CreateMockForUniquePtr<IAllocationsListener, MockAllocationListener>();
    auto manager = EtwEventsManager(allocationListener.get(), nullptr, nullptr, configuration.get());

    auto allocationPayload = CreateAllocationTickPayload(WStr("MyType"), true);
    // the buffer holds 3 frames (maxFrames == 3) but declares 4: one past the limit must be rejected
    auto stackWalkPayload = CreateStackWalkPayload(4, {0x1111, 0x2222, 0x3333});

    EXPECT_CALL(mockAllocationListener,
                OnAllocation(
                    _,
                    _,
                    _,
                    _,
                    "MyType",
                    _,
                    IsEmpty()));

    manager.OnEvent(etw_timestamp(132463843855875757), 2, 1, KEYWORD_GC, 1, EVENT_ALLOCATION_TICK, static_cast<uint32_t>(allocationPayload.size()), allocationPayload.data());
    manager.OnEvent(etw_timestamp(132463843855875800), 2, 1, KEYWORD_STACKWALK, 1, -1, static_cast<uint32_t>(stackWalkPayload.size()), stackWalkPayload.data());
}

TEST(EtwEventsManagerTest, ClrEventsMessageWithOversizedPayloadLengthIsIgnored)
{
    TestEtwEventsReceiver receiver;
    auto message = CreateClrEventsMessage(10, 1);

    SendPipeMessage(message.data(), static_cast<DWORD>(message.size()), receiver);

    EXPECT_EQ(0u, receiver.EventCount);
}

TEST(EtwEventsManagerTest, TruncatedClrEventsMessageIsIgnored)
{
    TestEtwEventsReceiver receiver;
    auto message = CreateClrEventsMessage(0, 0);

    SendPipeMessage(message.data(), static_cast<DWORD>(message.size() - 1), receiver);

    EXPECT_EQ(0u, receiver.EventCount);
}

TEST(EtwEventsManagerTest, ClrEventsMessageWithExactPayloadLengthIsForwarded)
{
    TestEtwEventsReceiver receiver;

    // declared payload length == bytes actually available after the header (userDataLength == availablePayload):
    // this exact-boundary case must be accepted and forwarded to the receiver.
    constexpr uint16_t payloadSize = 16;
    auto message = CreateClrEventsMessage(payloadSize, payloadSize);

    SendPipeMessage(message.data(), static_cast<DWORD>(message.size()), receiver);

    EXPECT_EQ(1u, receiver.EventCount);
    EXPECT_EQ(static_cast<uint32_t>(payloadSize), receiver.LastEventDataSize);
}

TEST(EtwEventsManagerTest, MessageSmallerThanIpcHeaderIsIgnored)
{
    TestEtwEventsReceiver receiver;

    // a message that does not even contain a full IpcHeader must be rejected before any field is read.
    std::array<uint8_t, sizeof(IpcHeader) - 1> buffer{};
    memcpy(buffer.data(), &DD_Ipc_Magic_V1, sizeof(DD_Ipc_Magic_V1));

    SendPipeMessage(buffer.data(), static_cast<DWORD>(buffer.size()), receiver);

    EXPECT_EQ(0u, receiver.EventCount);
}
#endif