// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#ifdef LINUX

#include "gtest/gtest.h"

#include "RingBuffer.h"
#include "SpinningMutex.hpp"

#include "OpSysTools.h"

TEST(RingBufferTest, CheckRingBufferSizing)
{
    // RingBuffer size = data size + metadata size
    // Metdatasize = page size

    //
    // for size < 4096, the ring buffer data size will be 4096
    auto pageSize = GetPageSize();
    auto metadataSize = GetPageSize();
    for (int i = 0; i <= pageSize; i++)
    {
        auto rb = RingBuffer(i, 1);
        ASSERT_EQ(rb.GetSize(), pageSize + metadataSize);
    }

    // if requested size for the ring buffer is power of 2,
    // check that we keep that size
    for (int i = pageSize; i < 1 << 15; i *= 2)
    {
        auto rb = RingBuffer(i, 1);
        ASSERT_EQ(rb.GetSize(), metadataSize + i);
    }

    // if request size is between 2 power of 2 integers,
    // check we use the bigger one
    for (int i = 4097, j = 8192; i < 1 << 15; i *= 2, j *= 2)
    {
        auto rb = RingBuffer(i, 1);
        ASSERT_EQ(rb.GetSize(), metadataSize + j);
    }
}

TEST(RingBufferTest, CheckByteBuffer)
{
    auto sampleSize = 25;
    auto rb = RingBuffer(1, sampleSize);
    auto w = rb.GetWriter();

    auto buffer = w.Reserve();
    ASSERT_EQ(buffer.size(), sampleSize);
    ASSERT_NE(buffer.data(), nullptr);

    for (std::uint8_t i = 0; i < sampleSize; i++)
    {
        buffer[i] = (std::byte)i;
    }

    w.Commit(buffer);

    auto r = rb.GetReader();
    ASSERT_EQ(r.AvailableSamples(), 1);

    auto readBuffer = r.GetNext();
    ASSERT_EQ(readBuffer.size(), sampleSize);
    ASSERT_NE(readBuffer.data(), nullptr);

    for (std::uint8_t i = 0; i < sampleSize; i++)
    {
        ASSERT_EQ(readBuffer[i], (std::byte)i);
    }
}

TEST(RingBufferTest, AddFakeSamplesAsMuchAsPossible)
{
    struct FakeSample
    {
    public:
        std::uint16_t _first;
        std::uint64_t _second;
        std::string _third;
        double _fourth;
    };

    // in a page we can store 102 samples (one sample = sizeof(FakeSample) + header size, all
    // of this aligned on 8 bytes)
    auto nbSamples = 102;
    auto rb = RingBuffer(4096, sizeof(FakeSample));

    for (int i = 0, j = 0; i < nbSamples; i++, j = 0)
    {
        auto w = rb.GetWriter();
        auto buffer = w.Reserve();
        ASSERT_FALSE(buffer.empty());

        auto sample = new (buffer.data()) FakeSample();

        auto rank = i * 10;
        sample->_first = rank + j++;
        sample->_second = rank + j++;
        sample->_third = std::to_string(rank + j++);
        sample->_fourth = rank + j++;

        {
            auto r = rb.GetReader();
            // even though the writer did not commit, the current sample
            // exist in the ring buffer but marked as 'busy'
            ASSERT_EQ(r.AvailableSamples(), i + 1);
        }

        w.Commit(buffer);
    }

    // make sure we cannot add more sample
    {
        auto w = rb.GetWriter();
        auto buffer = w.Reserve();
        ASSERT_TRUE(buffer.empty());
    }

    auto r = rb.GetReader();
    ASSERT_EQ(r.AvailableSamples(), nbSamples);

    for (int i = 0, j = 0; i < nbSamples; i++, j = 0)
    {
        auto buffer = r.GetNext();
        auto const* sample = reinterpret_cast<FakeSample const*>(buffer.data());

        auto rank = i * 10;
        ASSERT_EQ(sample->_first, rank + j++);
        ASSERT_EQ(sample->_second, rank + j++);
        ASSERT_EQ(sample->_third, std::to_string(rank + j++));
        ASSERT_EQ(sample->_fourth, rank + j++);
        // we have to release memory allocate by std::string
        std::destroy_at(const_cast<FakeSample*>(sample));
    }

    ASSERT_EQ(r.AvailableSamples(), 0);
}

TEST(RingBufferTest, StaleLock)
{
    auto rb = RingBuffer(42, 4);
    auto w = rb.GetWriter();

    rb.GetLock()->lock();
    auto timeout = false;

    ASSERT_TRUE(w.Reserve(&timeout).empty());
    ASSERT_TRUE(timeout);
}


TEST(RingBufferTest, CheckDiscard)
{
    auto sampleSize = 25;
    auto rb = RingBuffer(1, sampleSize);
    {
        auto w = rb.GetWriter();
        auto buffer = w.Reserve();
        ASSERT_EQ(buffer.size(), sampleSize);
        ASSERT_NE(buffer.data(), nullptr);

        for (std::uint8_t i = 0; i < sampleSize; i++)
        {
            buffer[i] = (std::byte)i;
        }

        w.Discard(buffer);

        auto r = rb.GetReader();
        // even if the sample was discarded, it's still present in the ring buffer
        ASSERT_EQ(r.AvailableSamples(), 1);
        // But reading into it will return an empty buffer
        ASSERT_TRUE(r.GetNext().empty());
    }

    // Make sure we still can reserve and use the ring buffer
    {
        auto w = rb.GetWriter();
        auto buffer = w.Reserve();
        ASSERT_EQ(buffer.size(), sampleSize);
        ASSERT_NE(buffer.data(), nullptr);

        for (std::uint8_t i = 0; i < sampleSize; i++)
        {
            buffer[i] = (std::byte)i;
        }

        w.Commit(buffer);

        auto r = rb.GetReader();
        ASSERT_EQ(r.AvailableSamples(), 1);
    }
}

struct MyStruct
{
    std::uint8_t ProducerId;
    double IthElement;
    std::uint16_t Other;
};

void writer_fun(RingBuffer* rb, std::uint64_t nbElements, std::size_t producerIdx)
{
    for(auto i = 0; i < nbElements; i++)
    {
        Buffer buffer;
        RingBuffer::Writer w;
        do
        {
            w = rb->GetWriter();
            buffer = w.Reserve();
            if (!buffer.empty())
            {
                break;
            }
            sched_yield();
        } while (true);

        ASSERT_EQ(buffer.size(), sizeof(MyStruct));
        auto* item = reinterpret_cast<MyStruct*>(buffer.data());

        item->ProducerId = producerIdx;
        item->IthElement = i;
        item->Other = i * producerIdx;

        w.Commit(buffer);
    }
}

void reader_fun(RingBuffer* rb, std::uint64_t nbElements, std::uint64_t nbProducers)
{
    auto totalReadElements = 0;

    std::vector<std::size_t> producers(nbProducers);

    while(totalReadElements < nbProducers * nbElements)
    {
        auto r = rb->GetReader();

        for (auto buffer = r.GetNext(); !buffer.empty(); buffer = r.GetNext())
        {
            auto* item = reinterpret_cast<const MyStruct*>(buffer.data());

            auto producerId = item->ProducerId;
            auto& count = producers[producerId];

            ASSERT_EQ(item->IthElement, count);
            ASSERT_EQ(item->Other, count * producerId);
            ++count;
            ++totalReadElements;
        }
    }
}

TEST(RingBufferTest, MultipleProducersSingleConsumer)
{
    auto nbElements = 1000;
    auto nbProducers = 8;

    auto rb = RingBuffer(4096, sizeof(MyStruct));
    std::vector<std::thread> threads;
    for (auto i = 0; i < nbProducers; i++)
    {
        threads.emplace_back(writer_fun, &rb, nbElements, i);
    }

    threads.emplace_back(reader_fun, &rb, nbElements, nbProducers);

    for(auto &thread : threads)
    {
        thread.join();
    }
}

#endif