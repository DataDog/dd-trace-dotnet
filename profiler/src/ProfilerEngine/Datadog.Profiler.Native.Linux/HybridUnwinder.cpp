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
            return -1;
        }
        context = &localContext;
    }

    unw_cursor_t cursor;
    auto result = unw_init_local2(&cursor, context, flag);

    if (result != 0)
    {
        return -1;
    }

    // === Phase 1: Walk native frames with libunwind ===
    // Push only native IPs. Stop when we reach managed code.
    std::size_t i = 0;
    unw_word_t ip = 0;
    bool isManagedIp = false;
    do
    {
        result = unw_get_reg(&cursor, UNW_REG_IP, &ip);
        if (result != 0 || ip == 0)
        {
            return i;
        }

        if (isManagedIp = _codeCache->IsManaged(ip); isManagedIp)
        {
            break;
        }

        buffer[i++] = ip;
        if (i >= bufferSize)
        {
            return i;
        }

        result = unw_step(&cursor);
    } while (result > 0);

    if (!isManagedIp)
    {
        return i;
    }
    if (i >= bufferSize)
    {
        return i;
    }

    // === Phase 2: Walk managed frames using FP chain ===
    // .NET JIT always emits frame pointer chains, so we switch
    // to manual FP walking once we've entered managed code.
    buffer[i++] = ip;
    if (i >= bufferSize)
    {
        return i;
    }

    bool hasStackBounds = (stackBase != 0) && (stackEnd != 0);

    unw_word_t fp = 0;
    if (hasStackBounds)
    {
        result = unw_get_reg(&cursor, UNW_REG_FP, &fp);
    }

    if (!hasStackBounds || result != 0 || !IsValidFp(fp, 0, stackBase, stackEnd))
    {
        return i;
    }

    // When libunwind encounters a frame without DWARF unwind info and no
    // discoverable frame record (e.g., a CLR leaf stub at the managed/native
    // boundary), it falls back to LR-only unwinding: IP is set from X30 but
    // X29 (FP) is left unchanged from the previous frame (see Gstep.c in
    // libunwind, "No frame record, fallback to link register" path).
    // This causes *(FP+8) to resolve back into the same function we already
    // pushed above. Detect and skip this stale frame.
    //
    // The LR-only fallback can be confirmed by observing fpBeforeStep == fpAfterStep
    // on the last unw_step iteration (FP unchanged across the step that landed on
    // managed code).
    auto firstLr = *reinterpret_cast<uintptr_t*>(fp + sizeof(void*));
    if (firstLr == ip)
    {
        uintptr_t staleFp = fp;
        fp = *reinterpret_cast<uintptr_t*>(fp);
        if (!IsValidFp(fp, staleFp, stackBase, stackEnd))
        {
            return i;
        }
    }

    uintptr_t prevFp = 0;
    do
    {
        ip = *reinterpret_cast<uintptr_t*>(fp + sizeof(void*));
        if (ip == 0) [[unlikely]]
        {
            break;
        }

        if (!_codeCache->IsManaged(ip))
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
    } while (i < bufferSize);

    return i;
}
