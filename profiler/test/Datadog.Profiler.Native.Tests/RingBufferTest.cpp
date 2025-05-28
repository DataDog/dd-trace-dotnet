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
        auto rb = RingBuffer(i);
        ASSERT_EQ(rb.GetSize(), pageSize + metadataSize);
    }

    // if requested size for the ring buffer is power of 2,
    // check that we keep that size
    for (int i = pageSize; i < 1 << 15; i *= 2)
    {
        auto rb = RingBuffer(i);
        ASSERT_EQ(rb.GetSize(), metadataSize + i);
    }

    // if request size is between 2 power of 2 integers,
    // check we use the bigger one
    for (int i = 4097, j = 8192; i < 1 << 15; i *= 2, j *= 2)
    {
        auto rb = RingBuffer(i);
        ASSERT_EQ(rb.GetSize(), metadataSize + j);
    }
}

TEST(RingBufferTest, CheckByteBuffer)
{
    auto rb = RingBuffer(1);
    auto w = rb.GetWriter();

    auto sampleSize = 25;
    auto buffer = w.Reserve(sampleSize);
    ASSERT_EQ(buffer.size(), sampleSize);
    ASSERT_NE(buffer.data(), nullptr);

    for (std::uint8_t i = 0; i < sampleSize; i++)
        buffer[i] = (std::byte)i;

    w.Commit(buffer);

    auto r = rb.GetReader();
    ASSERT_EQ(r.AvailableSamples(sampleSize), 1);

    auto readBuffer = r.Read();
    ASSERT_EQ(readBuffer.size(), sampleSize);
    ASSERT_NE(readBuffer.data(), nullptr);

    for (std::uint8_t i = 0; i < sampleSize; i++)
        ASSERT_EQ(readBuffer[i], (std::byte)i);
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
    auto rb = RingBuffer(4096);

    for (int i = 0, j = 0; i < nbSamples; i++, j = 0)
    {
        auto w = rb.GetWriter();
        auto buffer = w.Reserve(sizeof(FakeSample));
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
            ASSERT_EQ(r.AvailableSamples(sizeof(FakeSample)), i + 1);
        }

        w.Commit(buffer);
    }

    // make sure we cannot add more sample
    {
        auto w = rb.GetWriter();
        auto buffer = w.Reserve(sizeof(FakeSample));
        ASSERT_TRUE(buffer.empty());
    }

    auto r = rb.GetReader();
    ASSERT_EQ(r.AvailableSamples(sizeof(FakeSample)), nbSamples);

    for (int i = 0, j = 0; i < nbSamples; i++, j = 0)
    {
        auto buffer = r.Read();
        auto const* sample = reinterpret_cast<FakeSample const*>(buffer.data());

        auto rank = i * 10;
        ASSERT_EQ(sample->_first, rank + j++);
        ASSERT_EQ(sample->_second, rank + j++);
        ASSERT_EQ(sample->_third, std::to_string(rank + j++));
        ASSERT_EQ(sample->_fourth, rank + j++);
        // we have to release memory allocate by std::string
        std::destroy_at(const_cast<FakeSample*>(sample));
    }

    ASSERT_EQ(r.AvailableSamples(sizeof(FakeSample)), 0);
}

TEST(RingBufferTest, StaleLock)
{
    auto rb = RingBuffer(42);
    auto w = rb.GetWriter();

    rb.GetLock()->lock();
    auto timeout = false;

    ASSERT_TRUE(w.Reserve(4, &timeout).empty());
    ASSERT_TRUE(timeout);
}
#endif