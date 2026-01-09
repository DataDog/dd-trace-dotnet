// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gmock/gmock.h"
#include "gtest/gtest.h"

#include "ClrEventsParser.h"

#include "ProfilerMockedInterface.h"

#include <chrono>

using testing::_;
using testing::StrEq;

using namespace std::chrono_literals;

template <typename CharT>
class GenericStrEqMatcher : public testing::MatcherInterface<const CharT*>
{
public:
    explicit GenericStrEqMatcher(const std::basic_string<CharT>& expected) :
        expected_(expected)
    {
    }

    bool MatchAndExplain(const CharT* actual,
                         testing::MatchResultListener* listener) const override
    {
        if (actual == nullptr)
        {
            *listener << "which is null";
            return false;
        }
        return std::basic_string<CharT>(actual) == expected_;
    }

    void DescribeTo(std::ostream* os) const override
    {
        *os << "is equal to " << testing::PrintToString(expected_);
    }

    void DescribeNegationTo(std::ostream* os) const override
    {
        *os << "is not equal to " << testing::PrintToString(expected_);
    }

private:
    const std::basic_string<CharT> expected_;
};

template <typename CharT>
testing::Matcher<const CharT*> GenericStrEq(const std::basic_string<CharT>& str)
{
    return testing::MakeMatcher(new GenericStrEqMatcher<CharT>(str));
}

// Overloads for string literals
template <typename CharT>
testing::Matcher<const CharT*> GenericStrEq(const CharT* str)
{
    return GenericStrEq(std::basic_string<CharT>(str));
}

TEST(ClrEventsParserTest, ContentionStopV1)
{
    auto [contentionListener, mockContentionListener] = CreateMockForUniquePtr<IContentionListener, MockContentionListener>();
    auto parser = ClrEventsParser(nullptr, contentionListener.get(), nullptr, nullptr);

    auto expectedDuration = 21ns;
    ContentionStopV1Payload payload{0};

    payload.ContentionFlags = 0;
    payload.ClrInstanceId = 1;
    payload.DurationNs = static_cast<double_t>(expectedDuration.count());

    EXPECT_CALL(mockContentionListener, OnContention(expectedDuration));
    parser.ParseEvent(42ns, 1, KEYWORD_CONTENTION, EVENT_CONTENTION_STOP, sizeof(ContentionStopV1Payload), reinterpret_cast<LPCBYTE>(&payload));
}

TEST(ClrEventsParserTest, DoNothingForContentionStartV1)
{
    auto [contentionListener, mockContentionListener] = CreateMockForUniquePtr<IContentionListener, MockContentionListener>();
    auto parser = ClrEventsParser(nullptr, contentionListener.get(), nullptr, nullptr);

    EXPECT_CALL(mockContentionListener, SetBlockingThread(_)).Times(0);

    parser.ParseEvent(42ns, 1, KEYWORD_CONTENTION, EVENT_CONTENTION_START, 0, nullptr);
}

TEST(ClrEventsParserTest, ContentionStartV2)
{
    auto [contentionListener, mockContentionListener] = CreateMockForUniquePtr<IContentionListener, MockContentionListener>();
    auto parser = ClrEventsParser(nullptr, contentionListener.get(), nullptr, nullptr);

    std::uint64_t expectedOwnerThreadId = 43;

    ContentionStartV2Payload payload{0};
    payload.ContentionFlags = 0;
    payload.ClrInstanceId = 1;
    payload.LockId = 42;
    payload.AssociatedObjectID = 21;
    payload.LockOwnerThreadID = expectedOwnerThreadId;

    EXPECT_CALL(mockContentionListener, SetBlockingThread(expectedOwnerThreadId)).Times(1);

    parser.ParseEvent(1234ns, 2, KEYWORD_CONTENTION, EVENT_CONTENTION_START, sizeof(ContentionStartV2Payload), reinterpret_cast<LPCBYTE>(&payload));
}

// The CLR sends events with misalign field.
// In this case, since we create an event, it's ok to disregard the misalign issue
// reported by UBSAN
template <typename T>
uint64_t Write(std::uint8_t* buffer, std::uint64_t offset, T const& value)
#ifdef LINUX
__attribute__((no_sanitize("alignment")))
#endif
{
    *reinterpret_cast<T*>(buffer + offset) = value;
    return offset + sizeof(T);
}

std::pair<std::unique_ptr<std::uint8_t[]>, std::size_t> CreateAllocationTickEvent(
    WCHAR const* typeName,
    std::uint32_t kind,
    std::uint64_t allocationAmount,
    std::uintptr_t typeId,
    std::uintptr_t address,
    std::uint64_t objectSize)
{
    auto typeNbBytes = (std::char_traits<WCHAR>::length(typeName) + 1) * sizeof(WCHAR);
    std::size_t eventSize = sizeof(AllocationTickV4Payload) + typeNbBytes + 1;

    auto payload = std::make_unique<std::uint8_t[]>(eventSize);
    auto buffer = reinterpret_cast<AllocationTickV4Payload*>(payload.get());

    buffer->AllocationAmount = 21112; // do not care / ignored
    buffer->AllocationKind = kind;
    buffer->ClrInstanceId = 1; // do not care / ignored
    buffer->AllocationAmount64 = allocationAmount;
    buffer->TypeId = typeId;

    memcpy(&buffer->TypeName, typeName, typeNbBytes);

    std::uint64_t nextOffset =
        offsetof(AllocationTickV4Payload, TypeName) + typeNbBytes;

    nextOffset = Write<decltype(AllocationTickV4Payload::HeapIndex)>(payload.get(), nextOffset, 2); // do not care
    nextOffset = Write<decltype(AllocationTickV4Payload::Address)>(payload.get(), nextOffset, address);
    nextOffset = Write<decltype(AllocationTickV4Payload::ObjectSize)>(payload.get(), nextOffset, objectSize);

    return std::make_pair(std::move(payload), eventSize);
}

TEST(ClrEventsParserTest, AllocationTickV4)
{
    auto [allocationListener, mockAllocationListener] = CreateMockForUniquePtr<IAllocationsListener, MockAllocationListener>();
    auto parser = ClrEventsParser(allocationListener.get(), nullptr, nullptr, nullptr);

    auto typeName = WStr("MyType");
    auto [buffer, eventSize] = CreateAllocationTickEvent(typeName, 0x0, 42, 12, 123456789, 999);

    EXPECT_CALL(mockAllocationListener, OnAllocation(0, 12, GenericStrEq(typeName), 123456789, 999, 42)).Times(1);

    parser.ParseEvent(12345ns, 4, KEYWORD_GC, EVENT_ALLOCATION_TICK, (ULONG)eventSize, buffer.get());
}