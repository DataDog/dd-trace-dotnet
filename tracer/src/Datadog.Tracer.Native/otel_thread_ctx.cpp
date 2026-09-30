//---------------------------------------------------------------------------------------
// OTEP 4947 ("Thread Context: Sharing Thread-Level Information with External Readers")
// requires the publishing SDK to expose a pointer to the current thread's Thread-Local
// Context Record through a thread-local variable named `otel_thread_ctx_v1`, exported as
// an ELF TLS symbol (STT_TLS) in the dynamic symbol table, so out-of-process readers -
// such as the OpenTelemetry eBPF profiler - can resolve it by walking `/proc/<pid>/maps`
// and each mapped module's `.dynsym`.
//
// Native code owns record allocation and lifetime because managed [ThreadStatic] cleanup
// is not ordered with ELF TLS teardown. The managed tracer still owns the record layout
// and every hot-path write. See docs/OTelContextPropagation.md.
//
// NOTE: Must keep this signature in sync with the DllImport in NativeMethods.cs!
//
// This file is part of the SHARED production target rather than the static library, so
// that the TLS definition can never be dropped as an unreferenced archive member.
//---------------------------------------------------------------------------------------

#ifdef LINUX

#include "otel_thread_ctx.h"

#include <atomic>
#include <cstddef>
#include <cstdint>
#include <cstdlib>
#include <cstring>
#include <pthread.h>

// The project is compiled with `-fvisibility=hidden`, so the explicit default visibility
// below is what places the symbol in `.dynsym`. Datadog.Tracer.Native has no version
// script, so nothing filters it out afterwards.
//
// Per-thread 8-byte pointer to the 640-byte record itself.
extern "C"
{
    __attribute__((visibility("default"))) __thread void* otel_thread_ctx_v1;
}

namespace
{
constexpr std::size_t RecordSize = 640;
constexpr std::size_t RecordAlignment = 64;

// offset where the `valid` field is located, which is the only field that native code ever reads/writes
constexpr std::size_t ValidOffset = 24;
static_assert((RecordAlignment & (RecordAlignment - 1)) == 0);
static_assert(ValidOffset < RecordSize);

// The record layout is a cross-process, cross-language ABI whose source of truth is the managed
// OtelThreadContextRecord.cs. Native code only ever reads/writes `valid`, so everything before and
// after it is kept opaque.
struct OtelThreadContextRecord
{
    std::uint8_t              _prefix[ValidOffset]; // trace-id + span-id, opaque to native
    std::atomic<std::uint8_t> valid;
    std::uint8_t              _rest[RecordSize - ValidOffset - 1];
};
static_assert(sizeof(OtelThreadContextRecord) == RecordSize);
static_assert(offsetof(OtelThreadContextRecord, valid) == ValidOffset);

// A free Record reuses its own first 8 bytes as this 'next' link field. Those bytes overlap the record's
// trace-id, which is dead while the record is in the free list, so the list costs no extra memory.
//
//            same 640-byte allocation
//   +---------------------------------------------+
//   | byte0 ......................... byte639     |
//   +---------------------------------------------+
//
// WHILE RENTED (it's a record):
//   +--------+--------+---+---+------+--------------+
//   |trace_id|span_id |val|flg| size |  attrs_data  |
//   | 0..16  | 16..24 |24 |25 |26..28|   28..640    |
//   +--------+--------+---+---+------+--------------+
//
// WHILE IN THE FREE LIST (it's a FreeNode):
//   +--------+--------------------------------------+
//   | next   | unused garbage (nobody reads it)     |
//   | 0..8   | 8..640                               |
//   +--------+--------------------------------------+
//        |
//        +-> points to the next free record
//
struct FreeNode
{
    // the last node in the free list has next = nullptr
    FreeNode* next;
};
static_assert(sizeof(FreeNode) <= RecordSize);
static_assert(RecordAlignment % alignof(FreeNode) == 0);

// These objects have trivial process lifetime. In particular, the mutex is
// never destroyed: pthread key destructors may run while the process is shutting down.
pthread_mutex_t record_pool_lock = PTHREAD_MUTEX_INITIALIZER;

// Head of the free list. See FreeNode: each record links through its own first 8 bytes.
FreeNode* _free_list = nullptr;

pthread_once_t record_key_once = PTHREAD_ONCE_INIT;

// the key is the way to register a callback when the thread exits to return the record
pthread_key_t record_key;
int record_key_creation_result = -1;

OtelThreadContextRecord* RentRecord()
{
    OtelThreadContextRecord* pRecord = nullptr;

    // look for a record in the free list if any
    if (pthread_mutex_lock(&record_pool_lock) == 0)
    {
        FreeNode* pFreeNode = _free_list;
        if (pFreeNode != nullptr)
        {
            _free_list = pFreeNode->next;
            pRecord = reinterpret_cast<OtelThreadContextRecord*>(pFreeNode);
        }

        pthread_mutex_unlock(&record_pool_lock);
    }

    // free list is empty so it is needed to allocate one
    if (pRecord == nullptr)
    {
        void* pMemory = nullptr;
        // use posix_memalign to ensure the record is aligned to 64 bytes, which is required by the OTEP
        if (posix_memalign(&pMemory, RecordAlignment, RecordSize) != 0)
        {
            return nullptr;
        }

        pRecord = reinterpret_cast<OtelThreadContextRecord*>(pMemory);
    }

    // A fresh allocation is uninitialized and a recycled record still holds the previous owner's data
    // plus the stale FreeNode link in its first 8 bytes. So, zeroing everything here is what guarantees
    // a rented record is fully zeroed (including the 'valid field') before it is published.
    // The void* cast zeroes raw storage: the record is not yet a live object and holds an atomic member.
    std::memset(static_cast<void*>(pRecord), 0, RecordSize);
    return pRecord;
}

void ReturnRecord(OtelThreadContextRecord* pRecord)
{
    if (pRecord == nullptr)
    {
        return;
    }

    // valid = 0 is the reader gate: while the record sits on the free list, a reader that still
    // holds its address must see "no context" and skip it. We deliberately do NOT zero the rest
    // of the record here - it is unpublished, and RentRecord fully zeroes it before it is handed
    // out again, so a single scrub on rent covers both fresh and recycled records.
    pRecord->valid.store(0, std::memory_order_release);
    std::atomic_thread_fence(std::memory_order_seq_cst);

    if (pthread_mutex_lock(&record_pool_lock) != 0)
    {
        // The allocation intentionally remains live for the process lifetime. Failing to
        // recycle one record is safer than making thread teardown fail.
        return;
    }

    // The record is dead and unpublished, so its first 8 bytes are free to reuse as the link.
    FreeNode* pFreeNode = reinterpret_cast<FreeNode*>(pRecord);
    pFreeNode->next = _free_list;
    _free_list = pFreeNode;
    pthread_mutex_unlock(&record_pool_lock);
}

// called when the thread exits --> time to return the record to the pool
void DestroyThreadRecord(void* pThreadRecord)
{
    if (pThreadRecord == nullptr)
    {
        return;
    }

    auto* pRecord = static_cast<OtelThreadContextRecord*>(pThreadRecord);

    // First invalidate the record so that a reader with a pointer to it,
    // will see that it is invalid. Next, clear the discoverable slot before
    // returning the record to the free list
    pRecord->valid.store(0, std::memory_order_release);
    std::atomic_thread_fence(std::memory_order_seq_cst);
    __atomic_store_n(&otel_thread_ctx_v1, static_cast<void*>(nullptr), __ATOMIC_RELEASE);
    ReturnRecord(pRecord);
}

void CreateRecordKey()
{
    record_key_creation_result = pthread_key_create(&record_key, DestroyThreadRecord);
}
} // namespace

extern "C" __attribute__((visibility("default"))) void* GetOrCreateOtelThreadContextRecord()
{
    // ensure that the callback is registered to return the record to the free list when the thread exits
    if ((pthread_once(&record_key_once, CreateRecordKey) != 0 ) || (record_key_creation_result != 0))
    {
        return nullptr;
    }

    // if the thread already has a record, return it...
    if (void* pExisting = pthread_getspecific(record_key); pExisting != nullptr)
    {
        return pExisting;
    }

    //... otherwise, rent a new record and...
    OtelThreadContextRecord* pRecord = RentRecord();
    if (pRecord == nullptr)
    {
        return nullptr;
    }

    if (pthread_setspecific(record_key, pRecord) != 0)
    {
        ReturnRecord(pRecord);
        return nullptr;
    }

    // ...publish it as the thread-local variable so any reader can find it
    __atomic_store_n(&otel_thread_ctx_v1, static_cast<void*>(pRecord), __ATOMIC_RELEASE);
    return pRecord;
}

#endif
