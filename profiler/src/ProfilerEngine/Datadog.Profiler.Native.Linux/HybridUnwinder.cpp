// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "HybridUnwinder.h"
#include "ManagedCodeCache.h"

#define UNW_LOCAL_ONLY
#include <libunwind.h>

#ifndef ARM64
#error "HybridUnwinder is only supported on aarch64"
#endif

#define UNW_REG_FP UNW_AARCH64_X29

static inline bool IsValidFp(uintptr_t fp, uintptr_t prevFp,
                              uintptr_t stackBase, uintptr_t stackEnd)
{
    if (fp == 0)
    {
        return false;
    }

    if (fp % sizeof(void*) != 0)
    {
        return false;
    }

    if (fp < stackBase || fp >= stackEnd)
    {
        return false;
    }

    // Stack grows down on arm64: FP chain grows toward higher addresses
    if (prevFp != 0 && fp <= prevFp)
    {
        return false;
    }

    return true;
}

HybridUnwinder::HybridUnwinder(ManagedCodeCache* managedCodeCache)
    : _codeCache(managedCodeCache)
{
}

std::int32_t HybridUnwinder::Unwind(void* ctx, std::uintptr_t* buffer, std::size_t bufferSize,
                                    std::uintptr_t stackBase, std::uintptr_t stackEnd) const
{
    if (bufferSize == 0) [[unlikely]]
    {
        return 0;
    }

    auto* context = reinterpret_cast<unw_context_t*>(ctx);
    auto flag = static_cast<unw_init_local2_flags_t>(UNW_INIT_SIGNAL_FRAME);

    unw_context_t localContext;
    if (ctx == nullptr)
    {
        flag = static_cast<unw_init_local2_flags_t>(0);
        auto result = unw_getcontext(&localContext);
        if (result != 0)
        {
            // metric failed getting context
            return -1;
        }
        context = &localContext;
    }

    unw_cursor_t cursor;
    auto result = unw_init_local2(&cursor, context, flag);

    if (result != 0)
    {
        // metric failed initializing cursor
        return -1;
    }

    std::size_t i = 0;
    unw_word_t ip;
    do {
        result = unw_get_reg(&cursor, UNW_REG_IP, &ip);
        if (result != 0 || ip == 0)
        {
            // log/metric if result != 0
            return i;
        }
        buffer[i++] = ip;
        if (i >= bufferSize)
        {
            return i;
        }
        if (_codeCache->IsManaged(ip))
        {
            break;
        }
        result = unw_step(&cursor);
    } while (result > 0);

    // it was the last stack frame
    // or failed at moving forward
    // TODO log/metric this
    if (result <= 0)
    {
        // log/metric if result < 0
        return i;
    }
    if (i >= bufferSize)
    {
        return i;
    }

    // .NET JIT always emits frame pointer chains, so we can
    // switch to manual FP walking once we've entered managed code.
    bool hasStackBounds = (stackBase != 0) && (stackEnd != 0);

    // Only do manual FP walk when we have stack bounds to validate against.
    // Without bounds, we cannot safely dereference arbitrary pointers.
    if (!hasStackBounds)
    {
        return i;
    }

    unw_word_t fp;
    result = unw_get_reg(&cursor, UNW_REG_FP, &fp);
    if (result != 0)
    {
        // log/metric if result != 0
        return i;
    }

    uintptr_t prevFp = 0;

    if (!IsValidFp(fp, prevFp, stackBase, stackEnd))
    {
        // log/metric invalid fp
        return i;
    }

    do
    {
        ip = *reinterpret_cast<uintptr_t*>(fp + sizeof(void*));
        if (ip == 0) [[unlikely]]
        {
            break;
        }

        buffer[i++] = ip;
        prevFp = fp;
        fp = *reinterpret_cast<uintptr_t*>(fp);

        if (!IsValidFp(fp, prevFp, stackBase, stackEnd))
        {
            break;
        }

        if (!_codeCache->IsManaged(ip))
        {
            break;
        }
        // TODO check if we need ip validation too :thinking:
        // No risk of crash but more of data quality matter
    } while (i < bufferSize);

    return i;
}