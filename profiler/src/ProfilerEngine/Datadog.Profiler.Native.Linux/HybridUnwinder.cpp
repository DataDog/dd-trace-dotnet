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
        return false;

    if (fp % sizeof(void*) != 0)
        return false;

    // Ensure the full frame record [fp, fp+16) lies within the stack.
    if (fp < stackBase || fp + 2 * sizeof(void*) > stackEnd)
        return false;

    // Stack grows down on arm64: FP chain must grow toward higher addresses.
    if (prevFp != 0 && fp <= prevFp)
        return false;

    return true;
}

HybridUnwinder::HybridUnwinder(ManagedCodeCache* managedCodeCache) :
    _codeCache(managedCodeCache)
{
}

std::int32_t HybridUnwinder::Unwind(void* ctx, std::uintptr_t* buffer, std::size_t bufferSize,
                                    uintptr_t stackBase, uintptr_t stackEnd) const
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
        if (unw_getcontext(&localContext) != 0)
        {
            return -1;
        }
        context = &localContext;
    }

    unw_cursor_t cursor;
    if (unw_init_local2(&cursor, context, flag) != 0)
    {
        return -1;
    }

    // === Phase 1: Walk native frames with libunwind until managed code is reached ===
    std::size_t i = 0;
    unw_word_t ip = 0;
    while (true)
    {
        if (unw_get_reg(&cursor, UNW_REG_IP, &ip) != 0 || ip == 0)
        {
            return i;
        }

        if (_codeCache->IsManaged(ip))
            break;

        buffer[i++] = ip;
        if (i >= bufferSize)
        {
            return i;
        }

        if (unw_step(&cursor) <= 0)
        {
            return i;
        }
    }

    if (i >= bufferSize)
    {
        return i;
    }

    // === Phase 2: Walk managed frames using the FP chain ===
    // The .NET JIT on arm64 always emits a frame record [prev_fp, saved_lr] for
    // every managed method, so FP chaining is reliable once we enter managed code.
    buffer[i++] = ip;
    if (i >= bufferSize)
    {
        return i;
    }

    if (stackBase == 0 || stackEnd == 0)
    {
        return i;
    }

    unw_word_t fp = 0;
    if (unw_get_reg(&cursor, UNW_REG_FP, &fp) != 0 || !IsValidFp(fp, 0, stackBase, stackEnd))
    {
        return i;
    }

    // When libunwind falls back to LR-only (no DWARF, no frame record found for the
    // last native frame), IP is set from X30 but X29 (FP) is left unchanged, pointing
    // to that native frame rather than to the first managed frame.
    // That native frame's saved LR at [FP+8] is the raw return address into managed
    // code, which equals the raw cursor IP. Note: when ctx==nullptr (flag=0,
    // use_prev_instr=1), unw_get_reg(IP) returns cursor.ip-1, so we compare against
    // ip+1 to recover the raw value.
    const uintptr_t rawIp = (ctx == nullptr) ? (ip + 1) : ip;
    const auto firstLr = *reinterpret_cast<uintptr_t*>(fp + sizeof(void*));
    if (firstLr == rawIp)
    {
        const uintptr_t staleFp = fp;
        fp = *reinterpret_cast<uintptr_t*>(staleFp);
        if (!IsValidFp(fp, staleFp, stackBase, stackEnd))
        {
            return i;
        }
    }

    // Walk the FP chain, skipping non-managed (native/stub) frames.
    // In .NET 10+, user managed code calls throw via 3 native frames before reaching
    // the managed RhThrowEx:
    //   IL_Throw (asm stub) → IL_Throw_Impl (C++) → DispatchManagedException (C++) → RhThrowEx (managed)
    // In .NET 9, SoftwareExceptionFrame::Init() additionally calls PAL_VirtualUnwind(),
    // which adds 1–3 extra native frames, bringing the total to 5–6.
    // We must skip these native frames rather than stopping, or we lose the caller frame.
    // The limit of 8 consecutive non-managed frames (6 + 2 margin) stops useless walking
    // once we leave the managed portion of the stack entirely (e.g., thread startup code).
    uintptr_t prevFp = 0;
    int consecutiveNativeFrames = 0;
    while (true)
    {
        ip = *reinterpret_cast<uintptr_t*>(fp + sizeof(void*));
        if (ip == 0)
        {
            break;
        }

        if (_codeCache->IsManaged(ip))
        {
            if (i >= bufferSize)
            {
                break;
            }
            buffer[i++] = ip;
            consecutiveNativeFrames = 0;
        }
        else
        {
            if (++consecutiveNativeFrames > 8)
            {
                break;
            }
        }

        prevFp = fp;
        fp = *reinterpret_cast<uintptr_t*>(fp);
        if (!IsValidFp(fp, prevFp, stackBase, stackEnd))
        {
            break;
        }
    }

    return i;
}
