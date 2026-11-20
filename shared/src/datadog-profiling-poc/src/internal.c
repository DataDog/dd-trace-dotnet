#include "internal.h"

#include <stdarg.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#define DDOG_ERROR_BUFFER_SIZE 512

static DDOG_THREAD_LOCAL char g_last_error_message[DDOG_ERROR_BUFFER_SIZE];

ddog_error_code ddog__fail(ddog_error_code code, const char* fmt, ...)
{
    va_list args;
    va_start(args, fmt);
    vsnprintf(g_last_error_message, sizeof(g_last_error_message), fmt, args);
    va_end(args);
    return code;
}

const char* ddog_last_error_message(void)
{
    return g_last_error_message;
}

bool ddog__charslice_dup(ddog_charslice in, ddog_charslice* out_owned)
{
    if (in.len == 0 || in.ptr == NULL)
    {
        out_owned->ptr = NULL;
        out_owned->len = 0;
        return true;
    }

    char* copy = (char*)malloc(in.len);
    if (copy == NULL)
    {
        return false;
    }

    memcpy(copy, in.ptr, in.len);
    out_owned->ptr = copy;
    out_owned->len = in.len;
    return true;
}

void ddog__charslice_free(ddog_charslice* owned)
{
    if (owned == NULL)
    {
        return;
    }
    free((void*)owned->ptr);
    owned->ptr = NULL;
    owned->len = 0;
}

void ddog__buf_init(ddog__buf* buf)
{
    buf->data = NULL;
    buf->len = 0;
    buf->capacity = 0;
}

void ddog__buf_free(ddog__buf* buf)
{
    free(buf->data);
    buf->data = NULL;
    buf->len = 0;
    buf->capacity = 0;
}

static bool ddog__buf_reserve(ddog__buf* buf, size_t additional)
{
    size_t needed = buf->len + additional;
    if (needed <= buf->capacity)
    {
        return true;
    }

    size_t new_capacity = buf->capacity == 0 ? 256 : buf->capacity;
    while (new_capacity < needed)
    {
        new_capacity *= 2;
    }

    uint8_t* new_data = (uint8_t*)realloc(buf->data, new_capacity);
    if (new_data == NULL)
    {
        return false;
    }

    buf->data = new_data;
    buf->capacity = new_capacity;
    return true;
}

bool ddog__buf_append(ddog__buf* buf, const void* data, size_t len)
{
    if (len == 0)
    {
        return true;
    }
    if (!ddog__buf_reserve(buf, len))
    {
        return false;
    }
    memcpy(buf->data + buf->len, data, len);
    buf->len += len;
    return true;
}

bool ddog__buf_append_byte(ddog__buf* buf, uint8_t byte)
{
    return ddog__buf_append(buf, &byte, 1);
}
