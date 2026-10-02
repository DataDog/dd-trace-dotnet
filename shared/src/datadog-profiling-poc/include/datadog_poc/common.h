// PoC C replacement for libdatadog - see shared/src/datadog-profiling-poc/README.md
// This is NOT production code. It is a study of whether libdatadog's profiling
// upload path can be reimplemented in plain C with a minimal, hand-designed API.
#pragma once

#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>

#if defined(_WIN32)
#define DDOG_API __declspec(dllexport)
#else
#define DDOG_API __attribute__((visibility("default")))
#endif

#ifdef __cplusplus
extern "C" {
#endif

// ---- Error handling ----
// Every fallible function in this library returns a plain error code (0/DDOG_OK
// on success). Callers who want a human-readable detail on failure call
// ddog_last_error_message() immediately after - it is backed by a thread-local
// buffer that is only meaningful right after a non-DDOG_OK return, and is valid
// only until the next ddog_* call on the same thread.
typedef int32_t ddog_error_code;
#define DDOG_OK 0
#define DDOG_ERR_INVALID_ARGUMENT 1
#define DDOG_ERR_OUT_OF_MEMORY 2
#define DDOG_ERR_IO 3
#define DDOG_ERR_TRANSPORT 4

DDOG_API const char* ddog_last_error_message(void);

// ---- Borrowed string/byte slices ----
// Never owned by the callee; the pointed-to memory must outlive the call.
typedef struct {
    const char* ptr;
    size_t len;
} ddog_charslice;

typedef struct {
    const uint8_t* ptr;
    size_t len;
} ddog_byteslice;

// ---- Tags ----
// ddog_tag (the element) is opaque - its layout is private to tags.c and no
// caller ever dereferences it. ddog_vec_tag itself is a plain, fixed-size
// value (pointer/len/capacity, like a growable array header) that the caller
// owns and passes by pointer to be filled/mutated in place - no allocation
// for the header itself, only for its backing storage as tags are pushed.
typedef struct ddog_tag ddog_tag;

typedef struct {
    ddog_tag* ptr;
    size_t len;
    size_t capacity;
} ddog_vec_tag;

DDOG_API void ddog_vec_tag_new(ddog_vec_tag* out_tags);
DDOG_API ddog_error_code ddog_vec_tag_push(ddog_vec_tag* tags, ddog_charslice key, ddog_charslice value);
DDOG_API void ddog_vec_tag_drop(ddog_vec_tag* tags);

#ifdef __cplusplus
}
#endif
