// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "RingBuffer.h"

#include "Log.h"
#include "OpSysTools.h"
#include "SpinningMutex.hpp"

#include <errno.h>
#include <string.h>
#include <unistd.h>
#include <cassert>
#include <mutex>
#include <new>
#include <ostream>
#include <tuple>

#include <sys/mman.h>

using namespace std::chrono_literals;

std::atomic<std::uint8_t> RingBuffer::NbRingBuffers = 1;

struct RingBuffer::RingBufferImpl
{
public:
    std::uint64_t mask;
    std::size_t meta_size; // size of the metadata
    std::size_t data_size; // size of data
    std::byte* data;
    void* base;

    std::uint64_t* writer_pos;             // writer cursor
    std::uint64_t* reader_pos;             // reader cursor
    std::uint64_t intermediate_reader_pos; // intermediate_reader_pos > reader_pos,
                                           // part read by reader but not yet reflected
                                           // to the writer

    SpinningMutex* spinlock;
    int mapfd;
};

//  mimic: std::hardware_destructive_interference_size, C++17
inline constexpr std::size_t hardware_destructive_interference_size = 128;

struct RingBufferMetaDataPage
{
    alignas(hardware_destructive_interference_size) uint64_t writer_pos = 0;
    alignas(hardware_destructive_interference_size) uint64_t reader_pos = 0;
    alignas(hardware_destructive_interference_size) SpinningMutex spinlock;
};

std::size_t GetMetadataSize()
{
    // Use one page to store the metadata (RingBufferMetaDataPage)
    // in case in the future sizeof(RingBufferMetaDataPage) changes
    static_assert(sizeof(RingBufferMetaDataPage) <= DefaultPageSize);
    return GetPageSize();
}

// Return `x` rounded up to next multiple of `pow2`
// `pow2` must be a power of 2
// Return 0 for x==0 or x > std::numeric_limits<uint64_t>::max() - pow2 + 1
inline constexpr uint64_t align_up(uint64_t x, uint64_t pow2)
{
    assert(pow2 > 0 && (pow2 & (pow2 - 1)) == 0);
    return ((x - 1) | (pow2 - 1)) + 1;
}

std::size_t next_power_of_two(std::size_t size)
{
    if (size == 0) return 1;
    --size;
    size |= size >> 1;
    size |= size >> 2;
    size |= size >> 4;
    size |= size >> 8;
    size |= size >> 16;
    if (sizeof(size) == 8) size |= size >> 32;
    return size + 1;
}

std::size_t get_mask_from_size(size_t size)
{
    //assert(is_power_of_two(size));
    return (size - 1);
}

RingBuffer::RingBuffer(std::size_t size)
{
    std::string errorStr;
    std::tie(_impl, errorStr) = Create(size);
    if (_impl == nullptr)
    {
        Log::Error("Failed to create ring buffer: ", std::move(errorStr));
    }
}

static inline int memfd_create(const char *name, unsigned int flags) {
  return syscall(SYS_memfd_create, name, flags);
}

std::pair<RingBuffer::RingBufferUniquePtr, std::string> RingBuffer::Create(std::size_t requestedSize)
{
    auto rb = RingBufferUniquePtr(
        new RingBuffer::RingBufferImpl(),
        [](RingBuffer::RingBufferImpl* rb) {
            if (rb == nullptr)
            {
                return;
            }

            if (rb->mapfd >= 0)
            {
                close(rb->mapfd);
            }

            if (rb->base != nullptr)
            {
                auto const rbSize = rb->data_size + rb->meta_size;
                auto* ptr = reinterpret_cast<std::byte*>(rb->base);
                munmap(ptr + rbSize - rb->meta_size, rbSize);
                munmap(ptr, rbSize);
                munmap(ptr, 2 * rbSize - rb->meta_size);
            }

            delete rb;
        });


    auto rbName = "dd_profiler_ring_buffer_" + std::to_string(NbRingBuffers++);
    rb->mapfd = memfd_create(rbName.c_str(), 1U /*MFD_CLOEXEC*/);

    std::stringstream errorBuff;
    if (rb->mapfd < 0)
    {
        auto currentErrno = errno;
        errorBuff << "Failed to create file descriptor using memfd: " << strerror(currentErrno);
        return {nullptr, errorBuff.str()};
    }

    auto dataSize = next_power_of_two(std::max(GetPageSize(), requestedSize));
    auto const metadata_size = GetMetadataSize();
    auto rbSize = dataSize + metadata_size;

    if (ftruncate(rb->mapfd, rbSize) == -1)
    {
        auto currentErrno = errno;
        errorBuff << "Failed to set the size of the file descriptor (size=" << rbSize << "): " << strerror(currentErrno);
        return {nullptr, errorBuff.str()};
    }

    // Map in the region representing the ring buffer, map the buffer twice
    // (minus metadata size) to avoid handling boundaries.
    size_t const total_length = 2 * rbSize - metadata_size;
    // Reserve twice the size of the buffer
    void* base = mmap(nullptr, total_length, PROT_READ | PROT_WRITE,
                      MAP_PRIVATE | MAP_ANONYMOUS, -1, 0);

    if (MAP_FAILED == base || !base)
    {
        auto currentErrno = errno;
        errorBuff << "Failed to allocate virtual memory (" << total_length << " bytes): " << strerror(currentErrno);
        return {nullptr, errorBuff.str()};
    }

    auto* ptr = static_cast<std::byte*>(base);

    // Each mapping of fd must have a size of 2^n+1 pages
    // That's why starts by mapping buffer on the second half of reserved
    // space and ensure that metadata page overlaps on the first part
    if (mmap(ptr + dataSize, rbSize,
             PROT_READ | PROT_WRITE, MAP_SHARED | MAP_FIXED, rb->mapfd,
             0) == MAP_FAILED)
    {
        auto currentErrno = errno;
        errorBuff << "Failed to map the second half of the virtual memory: " << strerror(currentErrno);
        return {nullptr, errorBuff.str()};
    }

    // Map buffer a second time on the first half of reserved space
    // It will overlap the metadata page of the previous mapping.
    if (mmap(ptr, rbSize,
             PROT_READ | PROT_WRITE, MAP_SHARED | MAP_FIXED, rb->mapfd,
             0) == MAP_FAILED)
    {
        auto currentErrno = errno;
        errorBuff << "Failed to map the first half of the virtual memory: " << strerror(currentErrno);
        return {nullptr, errorBuff.str()};
    }

    rb->meta_size = metadata_size;
    rb->base = base;
    rb->data = reinterpret_cast<std::byte*>(base) + rb->meta_size;
    rb->data_size = dataSize;
    rb->mask = get_mask_from_size(dataSize);
    auto* meta = new (rb->base) RingBufferMetaDataPage();
    rb->reader_pos = &meta->reader_pos;
    rb->writer_pos = &meta->writer_pos;
    rb->intermediate_reader_pos = 0;
    rb->spinlock = new (&meta->spinlock) SpinningMutex();

    return {std::move(rb), ""};
}

RingBuffer::Writer RingBuffer::GetWriter()
{
    return Writer(_impl.get());
}

RingBuffer::Reader RingBuffer::GetReader()
{
    return Reader(_impl.get());
}

#ifdef DD_TEST
void RingBuffer::Reset()
{
    if (_impl == nullptr)
    {
        return;
    }

    // Reset the writer and reader positions
    *_impl->writer_pos = 0;
    *_impl->reader_pos = 0;
    _impl->intermediate_reader_pos = 0;
}

std::size_t RingBuffer::GetSize() const
{
    if (_impl == nullptr)
    {
        return 0;
    }
    return _impl->data_size + _impl->meta_size;
}

SpinningMutex* RingBuffer::GetLock()
{
    return _impl->spinlock;
}
#endif // DD_TEST

struct RingBufferHeader
{
    uint64_t size;
    static constexpr uint64_t k_discard_bit = 1UL << 62;
    static constexpr uint64_t k_busy_bit = 1UL << 63;

    static bool is_busy(uint64_t size)
    {
        return size & k_busy_bit;
    }
    static bool is_discarded(uint64_t size)
    {
        return size & k_discard_bit;
    }

    [[nodiscard]] size_t get_size() const
    {
        return size & ~k_discard_bit;
    }

    void set_busy()
    {
        size |= k_busy_bit;
    }
    void set_discarded()
    {
        size |= k_discard_bit;
    }
    [[nodiscard]] bool is_busy() const
    {
        return size & k_busy_bit;
    }
    [[nodiscard]] bool is_discarded() const
    {
        return size & k_discard_bit;
    }
};

inline constexpr size_t RingBufferAlignment{8};

RingBuffer::Writer::Writer(RingBufferImpl* rb) :
    _rb(rb)
{
}

Buffer RingBuffer::Writer::Reserve(std::size_t size, bool* timeout) const
{
    std::size_t const n2 =
        align_up(size + sizeof(RingBufferHeader), RingBufferAlignment);
    if (n2 == 0)
    {
        return {};
    }

    auto* rb = _rb;
    // \fixme{nsavoire} Not sure if spinlock is the best option here
    std::unique_lock const lock{*rb->spinlock, ReserveTimeout};
    if (!lock.owns_lock())
    {
        // timeout on lock
        if (timeout)
        {
            *timeout = true;
        }
        return {};
    }

    // No need for atomic operation, since we hold the lock
    std::uint64_t const writer_pos = *rb->writer_pos;

    std::uint64_t const new_writer_pos = writer_pos + n2;

    // Check that there is enough free space
    std::uint64_t tail = __atomic_load_n(_rb->reader_pos, __ATOMIC_ACQUIRE);
    if (rb->mask < new_writer_pos - tail)
    {
        return {};
    }

    uint64_t const head_linear = writer_pos & rb->mask;
    auto* hdr =
        reinterpret_cast<RingBufferHeader*>(rb->data + head_linear);

    // Mark the sample as busy
    hdr->size = size | RingBufferHeader::k_busy_bit;

    // Atomic operation required to synchronize with reader load_acquire
    __atomic_store_n(rb->writer_pos, new_writer_pos, __ATOMIC_RELEASE);

    return {reinterpret_cast<std::byte*>(hdr + 1), size};
}

void RingBuffer::Writer::Commit(Buffer buf)
{
    auto* hdr = reinterpret_cast<RingBufferHeader*>(buf.data()) - 1;

    // Clear busy bit
    std::uint64_t new_size = hdr->size ^ RingBufferHeader::k_busy_bit;
    // Needs release ordering to make sure that all previous writes are
    // visible to the reader once reader acquires `hdr->size`.
    __atomic_store_n(&hdr->size, new_size, __ATOMIC_RELEASE);
}

void RingBuffer::Writer::Discard(Buffer buf)
{
    auto* hdr = reinterpret_cast<RingBufferHeader*>(buf.data()) - 1;

    // Clear busy bit
    std::uint64_t new_size = hdr->size ^ RingBufferHeader::k_busy_bit;
    new_size |= RingBufferHeader::k_discard_bit;
    // Needs release ordering to make sure that all previous writes are
    // visible to the reader once reader acquires `hdr->size`.
    __atomic_store_n(&hdr->size, new_size, __ATOMIC_RELEASE);
}

RingBuffer::Reader::Reader(RingBufferImpl* rb) :
    _rb(rb)
{
    _head = __atomic_load_n(_rb->writer_pos, __ATOMIC_ACQUIRE);
    assert(rb->intermediate_reader_pos <= _head);
    assert(_rb->intermediate_reader_pos == *_rb->reader_pos);
}

RingBuffer::Reader::~Reader()
{
    if (_rb && *_rb->reader_pos < _rb->intermediate_reader_pos)
    {
        __atomic_store_n(_rb->reader_pos, _rb->intermediate_reader_pos,
                         __ATOMIC_RELEASE);
    }
}

std::size_t RingBuffer::Reader::AvailableSamples(std::size_t sampleSize) const
{
    return (_head - _rb->intermediate_reader_pos) / (sizeof(RingBufferHeader) + sampleSize);
}

ConstBuffer RingBuffer::Reader::Read()
{
    auto* rb = _rb;
    auto const head = _head;
    while (true)
    {
        auto tail = rb->intermediate_reader_pos;
        if (head == tail)
        {
            return {};
        }

        uint64_t const tail_linear = tail & rb->mask;
        std::byte* start = rb->data + tail_linear;
        auto* hdr = reinterpret_cast<RingBufferHeader*>(start);
        uint64_t const sz = __atomic_load_n(&hdr->size, __ATOMIC_ACQUIRE);

        // Sample not committed yet, bail out
        if (RingBufferHeader::is_busy(sz)) [[unlikely]]
        {
            return {};
        }

        rb->intermediate_reader_pos +=
            align_up((sz & ~RingBufferHeader::k_discard_bit) +
                         sizeof(RingBufferHeader),
                     RingBufferAlignment);

        if (RingBufferHeader::is_discarded(sz)) [[unlikely]]
        {
            continue;
        }

        return {reinterpret_cast<std::byte*>(hdr + 1), sz};
    }
}