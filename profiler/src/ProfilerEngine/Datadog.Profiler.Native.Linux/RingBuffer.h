// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <chrono>
#include <cstddef>
#include <cstdint>
#include <functional>
#include <memory>
#include <string>
#include <utility>

#include "shared/src/native-src/dd_span.hpp"

using Buffer = shared::span<std::byte>;
using ConstBuffer = shared::span<const std::byte>;

using namespace std::chrono_literals;

class SpinningMutex;

class RingBuffer
{
private:
    struct RingBufferImpl;
public:
    RingBuffer(std::size_t capacity, std::size_t sampleSize);
    ~RingBuffer() = default;

    RingBuffer(RingBuffer const&) = delete;
    RingBuffer& operator=(RingBuffer const&) = delete;
    RingBuffer(RingBuffer&&) = default;
    RingBuffer& operator=(RingBuffer&&) = default;

    class Writer
    {
    public:
        ~Writer() = default;

        #ifdef DD_TEST
        Writer() = default;
        #endif
        Writer(Writer const&) = delete;
        Writer& operator=(Writer const&) = delete;
        Writer(Writer&&) = default;
        Writer& operator=(Writer&&) = default;

        Buffer Reserve(bool* timeout = nullptr) const;
        void Commit(Buffer);
        void Discard(Buffer);

    private:
        friend class RingBuffer;
        explicit Writer(RingBufferImpl* rb);

        static constexpr std::chrono::milliseconds ReserveTimeout{100ms};
        RingBufferImpl* _rb;
    };

    class Reader
    {
    public:
        ~Reader();

        Reader(const Reader&) = delete;
        Reader& operator=(const Reader&) = delete;
        Reader(Reader&&) = default;
        Reader& operator=(Reader&&) = default;

        // sampleSize allows to compute the number of samples hold by the reader
        std::size_t AvailableSamples() const;
        ConstBuffer GetNext();

    private:
        friend class RingBuffer;
        explicit Reader(RingBufferImpl* rb);

        RingBufferImpl* _rb;
        uint64_t _tail;
    };

    Writer GetWriter();
    Reader GetReader();

#ifdef DD_TEST
    void Reset();
    std::size_t GetSize() const;
    SpinningMutex* GetLock();
#endif // DD_TEST

private:
    using RingBufferUniquePtr = std::unique_ptr<RingBufferImpl, std::function<void(RingBufferImpl*)>>;
    static std::pair<RingBufferUniquePtr, std::string> Create(std::size_t capacity, std::size_t sampleSize);

    static std::atomic<std::uint8_t> NbRingBuffers;

    RingBufferUniquePtr _impl;
};