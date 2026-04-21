// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "HybridUnwinder.h"
#include "ManagedCodeCache.h"

#include "UnwinderTracer.h"

#include "FrameStore.h"

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

// This is temporary workaround to try at best identifying an instruction pointer.
// Once ManagedCodeCache has a better concurrent data structure, we can remove this function.
std::optional<bool> HybridUnwinder::IsManaged(uintptr_t ip) const
{
    // best effort to get the managed code address range
    // If IsManaged returns nullopt (which means that we failed at acquiring the lock),
    // we try 3 times.
    const std::size_t MaxRetries = 3;
    for (auto i = 0; i < MaxRetries; i++)
    {
        auto isManaged = _codeCache->IsManaged(ip);
        if (isManaged.has_value())
        {
            return isManaged;
        }
    }
    return std::nullopt;
}

struct UnwindCursor
{
    unw_cursor_t cursor;
};

bool HybridUnwinder::UnwindNativeFrames(UnwindCursor* cursor, std::uintptr_t* buffer, std::size_t bufferSize,
    ManagedCodeCache* managedCodeCache,
                        UnwinderTracer* tracer, std::size_t& i) const
{
    unw_word_t ip = 0;
    while (true)
    {
        if (i >= bufferSize)
        {
            if (tracer) tracer->RecordFinish(static_cast<std::int32_t>(i), FinishReason::BufferFull);
            return false;
        }

        if (auto getResult = unw_get_reg(&cursor->cursor, UNW_REG_IP, &ip); getResult != 0 || ip == 0)
        {
            if (tracer) tracer->RecordFinish(getResult, FinishReason::FailedGetReg);
            return false;
        }

        auto isManaged = IsManaged(ip);
        if (isManaged.has_value())
        {
            if (isManaged.value())
            {
                if (tracer)
                {
                    unw_word_t managedFp = 0;
                    unw_get_reg(&cursor->cursor, UNW_REG_FP, &managedFp);
                    tracer->Record(EventType::ManagedTransition, ip, managedFp);
                }
                break;
            }
        }
        else
        {
            buffer[i++] = FrameStore::FakeUnknownIP;
            if (tracer)
            {
                tracer->RecordFinish(static_cast<std::int32_t>(i), FinishReason::FailedIsManaged);
            }
            return false;
        }

        if (tracer)
        {
            unw_word_t sp = 0;
            unw_word_t nativeFp = 0;
            unw_get_reg(&cursor->cursor, UNW_AARCH64_SP, &sp);
            unw_get_reg(&cursor->cursor, UNW_REG_FP, &nativeFp);
            tracer->Record(EventType::NativeFrame, ip, nativeFp, sp);
        }

        buffer[i++] = ip;

        auto stepResult = unw_step(&cursor->cursor);
        unw_cursor_snapshot_t snapshot;
        unw_get_cursor_snapshot(&cursor->cursor, &snapshot);
        if (tracer) tracer->Record(EventType::LibunwindStep, stepResult, snapshot);
        if (stepResult <= 0)
        {
            if (tracer) tracer->RecordFinish(static_cast<std::int32_t>(i), FinishReason::FailedLibunwindStep);
            return false;
        }
    }

    return true;
}

bool HybridUnwinder::UnwindManagedFrames(UnwindCursor* cursor, std::uintptr_t* buffer, std::size_t bufferSize,
    ManagedCodeCache* managedCodeCache,
                        UnwinderTracer* tracer, std::size_t& i,
                        std::uintptr_t stackBase, std::uintptr_t stackEnd) const
{
    if (i >= bufferSize)
    {
        if (tracer) tracer->RecordFinish(static_cast<std::int32_t>(i), FinishReason::BufferFull);
        return false;
    }

    unw_word_t ip = 0;
    if (auto result = unw_get_reg(&cursor->cursor, UNW_REG_IP, &ip); result != 0 || ip == 0)
    {
        if (tracer) tracer->RecordFinish(static_cast<std::int32_t>(UNW_REG_IP), FinishReason::FailedGetReg);
        return false;
    }

    buffer[i++] = ip;

    unw_word_t fp = 0;
    if (auto result = unw_get_reg(&cursor->cursor, UNW_REG_FP, &fp); result != 0 || !IsValidFp(fp, 0, stackBase, stackEnd))
    {
        if (tracer) tracer->RecordFinish(static_cast<std::int32_t>(i), FinishReason::InvalidFp);
        return false;
    }

    unw_word_t lr = 0;
    auto lrResult = unw_get_reg(&cursor->cursor, UNW_AARCH64_X30, &lr);
    if (lrResult == 0)
    {
        const auto savedLr = *reinterpret_cast<uintptr_t*>(fp + sizeof(void*));
        if (lr != savedLr)
        {
            auto lrIsManaged = IsManaged(lr);
            if (!lrIsManaged.has_value())
            {
                buffer[i++] = 0x42; // Unknown managed function
            }
            else if (lrIsManaged.value())
            {
                buffer[i++] = lr; // Managed function
            }
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
    FinishReason finishReason = FinishReason::Success;
    while (true)
    {
        if (i >= bufferSize)
        {
            finishReason = FinishReason::BufferFull;
            break;
        }

        auto ip = *reinterpret_cast<uintptr_t*>(fp + sizeof(void*));
        if (ip == 0)
        {
            break;
        }

        if (tracer) tracer->Record(EventType::FrameChainStep, ip, fp);

        auto isManaged = IsManaged(ip);
        if (!isManaged.has_value() || isManaged.value())
        //if (isManaged.has_value() && isManaged.value())
        {
            if (!isManaged.has_value())
            {
                buffer[i++] = 0x42; // Unknown managed function
            }
            else
            {
                buffer[i++] = ip;
                consecutiveNativeFrames = 0;
            }
        }
        else if (!isManaged.value())
        {
            static constexpr std::size_t MaxConsecutiveNativeFrames = 9;
            if (++consecutiveNativeFrames > MaxConsecutiveNativeFrames)
            {
                finishReason = FinishReason::TooManyNativeFrames;
                break;
            }
        }

        prevFp = fp;
        fp = *reinterpret_cast<uintptr_t*>(fp);
        if (!IsValidFp(fp, prevFp, stackBase, stackEnd))
        {
            finishReason = FinishReason::InvalidFp;
            break;
        }
    }
    if (tracer) tracer->RecordFinish(static_cast<std::int32_t>(i), finishReason);
    return true;
}

std::int32_t HybridUnwinder::Unwind(void* ctx, std::uintptr_t* buffer, std::size_t bufferSize,
                                    uintptr_t stackBase, uintptr_t stackEnd,
                                    UnwinderTracer* tracer) const
{
    if (bufferSize == 0) [[unlikely]]
    {
        return 0;
    }

    if (tracer) tracer->RecordStart(reinterpret_cast<ucontext_t*>(ctx));

    if (stackBase == 0 || stackEnd == 0)
    {
        if (tracer) tracer->RecordFinish(0, FinishReason::NoStackBounds);
        return 0;
    }

    auto* context = reinterpret_cast<unw_context_t*>(ctx);
    auto flag = static_cast<unw_init_local2_flags_t>(UNW_INIT_SIGNAL_FRAME);

    unw_context_t localContext;
    if (ctx == nullptr)
    {
        flag = static_cast<unw_init_local2_flags_t>(0);
        if (auto getResult = unw_getcontext(&localContext) != 0)
        {
            if (tracer) tracer->RecordFinish(getResult, FinishReason::FailedGetContext);
            return -1;
        }
        context = &localContext;
    }

    UnwindCursor unwindCursor{0};
    auto initResult = unw_init_local2(&unwindCursor.cursor, context, flag);
    unw_cursor_snapshot_t snapshot= {0};
    unw_get_cursor_snapshot(&unwindCursor.cursor, &snapshot);
    if (tracer) tracer->Record(EventType::InitCursor, initResult, snapshot);
    if (initResult != 0)
    {
        if (tracer) tracer->RecordFinish(initResult, FinishReason::FailedInitLocal2);
        return -1;
    }

    // === Phase 1: Walk native frames with libunwind until managed code is reached ===
    std::size_t i = 0;
    auto keepOnUnwinding = UnwindNativeFrames(&unwindCursor, buffer, bufferSize, _codeCache, tracer, i);
    if (!keepOnUnwinding)
    {
        // already recorded state
        return i;
    }

    if (i >= bufferSize)
    {
        if (tracer) tracer->RecordFinish(static_cast<std::int32_t>(i), FinishReason::BufferFull);
        return i;
    }

    // === Phase 2: Walk managed frames using the FP chain ===
    // The .NET JIT on arm64 always emits a frame record [prev_fp, saved_lr] for
    // every managed method, so FP chaining is reliable once we enter managed code.
    // buffer[i++] = ip;
    // if (i >= bufferSize)
    // {
    //     if (tracer) tracer->RecordFinish(static_cast<std::int32_t>(i), FinishReason::BufferFull);
    //     return i;
    // }

    auto _ = UnwindManagedFrames(&unwindCursor, buffer, bufferSize, _codeCache, tracer, i, stackBase, stackEnd);

    // Already recorded state in tracer
    return i;
}
