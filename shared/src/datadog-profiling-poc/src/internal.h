// Private helpers shared across this library's .c files. Not part of the
// public API in include/datadog_poc/ - never installed, never seen by callers.
#pragma once

#include "datadog_poc/common.h"

#if defined(_MSC_VER)
#define DDOG_THREAD_LOCAL __declspec(thread)
#else
#define DDOG_THREAD_LOCAL _Thread_local
#endif

// Formats a message into the thread-local last-error buffer (read back via
// the public ddog_last_error_message()) and returns `code`, so call sites can
// write `return ddog__fail(DDOG_ERR_INVALID_ARGUMENT, "profile is NULL");`.
ddog_error_code ddog__fail(ddog_error_code code, const char* fmt, ...);

// Heap-copies a possibly-non-NUL-terminated slice into an owned, NUL-terminated
// buffer. A zero-length slice yields {NULL, 0} without allocating. Returns
// false on allocation failure (out_slice left untouched).
bool ddog__charslice_dup(ddog_charslice in, ddog_charslice* out_owned);
void ddog__charslice_free(ddog_charslice* owned);

// Minimal growable byte buffer, used to build both the raw pprof bytes and
// the JSON "event" part without repeated manual realloc bookkeeping at every
// call site.
typedef struct {
    uint8_t* data;
    size_t len;
    size_t capacity;
} ddog__buf;

void ddog__buf_init(ddog__buf* buf);
void ddog__buf_free(ddog__buf* buf);
bool ddog__buf_append(ddog__buf* buf, const void* data, size_t len);
bool ddog__buf_append_byte(ddog__buf* buf, uint8_t byte);
