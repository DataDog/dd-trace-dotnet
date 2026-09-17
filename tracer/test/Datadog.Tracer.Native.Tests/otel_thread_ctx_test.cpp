#include "pch.h"

#ifdef LINUX

#include "../../src/Datadog.Tracer.Native/otel_thread_ctx.h"

#include <atomic>
#include <cstddef>
#include <cstdint>
#include <cstring>
#include <pthread.h>
#include <sched.h>

namespace
{
constexpr std::size_t RecordSize = 640;
constexpr std::size_t RecordAlignment = 64;
constexpr std::size_t ThreadCount = 16;

struct RecordObservation
{
    void* Address = nullptr;
    bool AddressIsStable = false;
    bool RecordWasZeroed = false;
};

void* AcquireAndDirtyRecord(void* state)
{
    auto* observation = static_cast<RecordObservation*>(state);
    observation->Address = GetOrCreateOtelThreadContextRecord();
    observation->AddressIsStable = observation->Address == GetOrCreateOtelThreadContextRecord();

    if (observation->Address == nullptr)
    {
        return nullptr;
    }

    auto* bytes = static_cast<std::uint8_t*>(observation->Address);
    observation->RecordWasZeroed = true;

    for (std::size_t i = 0; i < RecordSize; i++)
    {
        if (bytes[i] != 0)
        {
            observation->RecordWasZeroed = false;
            break;
        }
    }

    std::memset(observation->Address, 0xab, RecordSize);
    return nullptr;
}

struct ConcurrentRecordState
{
    std::atomic<std::size_t> Ready{0};
    std::atomic<bool> Release{false};
};

struct ConcurrentRecordObservation
{
    ConcurrentRecordState* State;
    void* Address = nullptr;
};

void* AcquireAndWait(void* state)
{
    auto* observation = static_cast<ConcurrentRecordObservation*>(state);
    observation->Address = GetOrCreateOtelThreadContextRecord();
    observation->State->Ready.fetch_add(1, std::memory_order_release);

    while (!observation->State->Release.load(std::memory_order_acquire))
    {
        sched_yield();
    }

    return nullptr;
}
} // namespace

TEST(OtelThreadContextTest, RecyclesAZeroedRecordAfterTheOwningThreadExits)
{
    RecordObservation first;
    pthread_t first_thread;
    ASSERT_EQ(0, pthread_create(&first_thread, nullptr, AcquireAndDirtyRecord, &first));
    ASSERT_EQ(0, pthread_join(first_thread, nullptr));

    ASSERT_NE(nullptr, first.Address);
    EXPECT_TRUE(first.AddressIsStable);
    EXPECT_TRUE(first.RecordWasZeroed);
    EXPECT_EQ(0, reinterpret_cast<std::uintptr_t>(first.Address) % RecordAlignment);

    RecordObservation second;
    pthread_t second_thread;
    ASSERT_EQ(0, pthread_create(&second_thread, nullptr, AcquireAndDirtyRecord, &second));
    ASSERT_EQ(0, pthread_join(second_thread, nullptr));

    ASSERT_NE(nullptr, second.Address);
    EXPECT_TRUE(second.AddressIsStable);
    EXPECT_TRUE(second.RecordWasZeroed);
    EXPECT_EQ(first.Address, second.Address);
}

TEST(OtelThreadContextTest, HandsOutDistinctRecordsToConcurrentThreads)
{
    ConcurrentRecordState state;
    pthread_t threads[ThreadCount];
    ConcurrentRecordObservation observations[ThreadCount];
    std::size_t created = 0;

    for (std::size_t i = 0; i < ThreadCount; i++)
    {
        observations[i].State = &state;
        auto result = pthread_create(&threads[i], nullptr, AcquireAndWait, &observations[i]);

        if (result != 0)
        {
            state.Release.store(true, std::memory_order_release);

            for (std::size_t j = 0; j < created; j++)
            {
                pthread_join(threads[j], nullptr);
            }

            FAIL() << "pthread_create failed with " << result;
            return;
        }

        created++;
    }

    while (state.Ready.load(std::memory_order_acquire) != ThreadCount)
    {
        sched_yield();
    }

    bool all_records_are_valid = true;
    bool all_records_are_distinct = true;

    for (std::size_t i = 0; i < ThreadCount; i++)
    {
        all_records_are_valid &= observations[i].Address != nullptr;

        for (std::size_t j = 0; j < i; j++)
        {
            all_records_are_distinct &= observations[j].Address != observations[i].Address;
        }
    }

    state.Release.store(true, std::memory_order_release);

    for (std::size_t i = 0; i < ThreadCount; i++)
    {
        EXPECT_EQ(0, pthread_join(threads[i], nullptr));
    }

    EXPECT_TRUE(all_records_are_valid);
    EXPECT_TRUE(all_records_are_distinct);
}

#endif
