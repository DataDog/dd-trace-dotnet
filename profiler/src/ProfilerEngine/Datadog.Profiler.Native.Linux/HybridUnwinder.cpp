// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "HybridUnwinder.h"

#include "Callstack.h"
#include "ManagedCodeCache.h"

#include "UnwindingRecorder.h"

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

struct UnwindCursor
{
    unw_cursor_t cursor;
};

bool HybridUnwinder::UnwindNativeFrames(UnwindCursor* cursor, Callstack& callstack,
    UnwindingRecorder* recorder) const
{
    unw_word_t ip = 0;
    while (true)
    {
        if (auto getResult = unw_get_reg(&cursor->cursor, UNW_REG_IP, &ip); getResult != 0 || ip == 0)
        {
            if (recorder)
            {
                recorder->RecordFinish(getResult, FinishReason::FailedGetReg);
            }
            return false;
        }

        auto isManaged = _codeCache->IsManaged(ip);
        if (isManaged.has_value())
        {
            if (isManaged.value())
            {
                if (recorder)
                {
                    unw_word_t managedFp = 0;
                    unw_get_reg(&cursor->cursor, UNW_REG_FP, &managedFp);
                    recorder->Record(EventType::ManagedTransition, ip, managedFp);
                }
                break;
            }
        }
        else
        {
            callstack.Add(FrameStore::UnknownFrameTypeIP);
            if (recorder)
            {
                recorder->RecordFinish(static_cast<std::int32_t>(callstack.Size()), FinishReason::FailedIsManaged);
            }
            return false;
        }

        if (recorder)
        {
            unw_word_t sp = 0;
            unw_word_t nativeFp = 0;
            unw_get_reg(&cursor->cursor, UNW_AARCH64_SP, &sp);
            unw_get_reg(&cursor->cursor, UNW_REG_FP, &nativeFp);
            recorder->Record(EventType::NativeFrame, ip, nativeFp, sp);
        }

        if (!callstack.Add(ip))
        {
            if (recorder) recorder->RecordFinish(static_cast<std::int32_t>(callstack.Size()), FinishReason::BufferFull);
            return false;
        }

        auto stepResult = unw_step(&cursor->cursor);
        if (recorder)
        {
            unw_cursor_snapshot_t snapshot;
            unw_get_cursor_snapshot(&cursor->cursor, &snapshot);
            recorder->Record(EventType::LibunwindStep, stepResult, snapshot);
        }
        if (stepResult <= 0)
        {
            if (recorder)
            {
                recorder->RecordFinish(static_cast<std::int32_t>(stepResult), FinishReason::FailedLibunwindStep);
            }
            return false;
        }
    }

    return true;
}

void HybridUnwinder::UnwindManagedFrames(UnwindCursor* cursor, Callstack& callstack,
    UnwindingRecorder* recorder,
    std::uintptr_t stackBase, std::uintptr_t stackEnd) const
{
    unw_word_t ip = 0;
    if (auto result = unw_get_reg(&cursor->cursor, UNW_REG_IP, &ip); result != 0 || ip == 0)
    {
        if (recorder)
        {
            recorder->RecordFinish(static_cast<std::int32_t>(UNW_REG_IP), FinishReason::FailedGetReg);
        }
        return;
    }

    if (!callstack.Add(ip))
    {
        if (recorder)
        {
            recorder->RecordFinish(static_cast<std::int32_t>(callstack.Size()), FinishReason::BufferFull);
        }
        return;
    }

    unw_word_t fp = 0;
    if (auto result = unw_get_reg(&cursor->cursor, UNW_REG_FP, &fp); result != 0 || !IsValidFp(fp, 0, stackBase, stackEnd))
    {
        if (recorder)
        {
            recorder->RecordFinish(static_cast<std::int32_t>(result), FinishReason::InvalidFp);
        }
        return;
    }

    // For now we do not handle leaf function case.
    // This is a TODO:
    // The reason is that we may duplicate top frame in some cases.
    // Instead, in a follow up PR, we will give unwinding info to the unwinder
    // to make the callstack collection more accurate.

    // Walk the FP chain.
    // In .NET 10+, user managed code calls throw via 3 native frames before reaching
    // the managed RhThrowEx:
    //   IL_Throw (asm stub) -> IL_Throw_Impl (C++) -> DispatchManagedException (C++) -> RhThrowEx (managed)
    // In .NET 9, SoftwareExceptionFrame::Init() additionally calls PAL_VirtualUnwind(),
    // which adds 1-3 extra native frames, bringing the total to 5-6.
    // We must skip these native frames rather than stopping, or we lose the caller frame.
    // The limit of 8 consecutive non-managed frames (6 + 2 margin) stops useless walking
    // once we leave the managed portion of the stack entirely (e.g., thread startup code).
    uintptr_t prevFp = 0;
    int consecutiveNativeFrames = 0;
    FinishReason finishReason = FinishReason::Success;
    while (true)
    {
        auto ip = *reinterpret_cast<uintptr_t*>(fp + sizeof(void*));
        if (ip == 0)
        {
            // We hit the bottom of the stack. Most of the time, ip == 0  means end of calls
            break;
        }

        if (recorder) recorder->Record(EventType::FrameChainStep, ip, fp);

        auto isManaged = _codeCache->IsManaged(ip);
        if (isManaged.has_value() && isManaged.value())
        {
            if (!callstack.Add(ip))
            {
                if (recorder)
                {
                    recorder->RecordFinish(static_cast<std::int32_t>(callstack.Size()), FinishReason::BufferFull);
                }
                break;
            }
            consecutiveNativeFrames = 0;
        }
        else
        {
            static constexpr std::size_t MaxConsecutiveNativeFrames = 8;
            // In case we were unable to identify, we assume it's a managed frame
            if (!isManaged.has_value())
            {
                if (!callstack.Add(FrameStore::FakeUnknownIP))
                {
                    if (recorder)
                    {
                        recorder->RecordFinish(static_cast<std::int32_t>(callstack.Size()), FinishReason::BufferFull);
                    }
                    break;
                }
            }
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
    if (recorder)
    {
        recorder->RecordFinish(static_cast<std::int32_t>(callstack.Size()), finishReason);
    }
    return;
}

std::int32_t HybridUnwinder::Unwind(void* ctx, Callstack& callstack,
                                    uintptr_t stackBase, uintptr_t stackEnd,
                                    UnwindingRecorder* recorder) const
{
    if (recorder)
    {
        recorder->RecordStart(reinterpret_cast<ucontext_t*>(ctx));
    }

    if (stackBase == 0 || stackEnd == 0)
    {
        if (recorder)
        {
            recorder->RecordFinish(0, FinishReason::NoStackBounds);
        }
        return 0;
    }

    auto* context = reinterpret_cast<unw_context_t*>(ctx);
    auto flag = static_cast<unw_init_local2_flags_t>(UNW_INIT_SIGNAL_FRAME);

    unw_context_t localContext;
    if (ctx == nullptr)
    {
        flag = static_cast<unw_init_local2_flags_t>(0);
        if (auto getResult = unw_getcontext(&localContext); getResult  != 0)
        {
            if (recorder)
            {
                recorder->RecordFinish(getResult, FinishReason::FailedGetContext);
            }
            return -1;
        }
        context = &localContext;
    }

    UnwindCursor unwindCursor{};
    auto initResult = unw_init_local2(&unwindCursor.cursor, context, flag);
    if (recorder)
    {
        unw_cursor_snapshot_t snapshot= {0};
        unw_get_cursor_snapshot(&unwindCursor.cursor, &snapshot);
        recorder->Record(EventType::InitCursor, initResult, snapshot);
    }
    if (initResult != 0)
    {
        if (recorder)
        {
            recorder->RecordFinish(initResult, FinishReason::FailedInitLocal2);
        }
        return -1;
    }

    // === Phase 1: Walk native frames with libunwind until managed code is reached ===
    auto keepOnUnwinding = UnwindNativeFrames(&unwindCursor, callstack, recorder);
    if (!keepOnUnwinding)
    {
        // already recorded state
        return callstack.Size();
    }

    // DEBUG: inject a sentinel between Phase 1 (native) and Phase 2 (managed).
    // If the test output shows a managed frame ABOVE this sentinel, it means
    // Phase 1 misclassified it as native (code cache race).
    callstack.Add(FrameStore::SentinelFrameIP);
    // buffer[i++] = FrameStore::SentinelFrameIP;
    // if (i >= bufferSize)
    // {
    //     return i;
    // }

    // === Phase 2: Walk managed frames using the FP chain ===
    // The .NET JIT on arm64 always emits a frame record [prev_fp, saved_lr] for
    // every managed method, so FP chaining is reliable once we enter managed code.

    UnwindManagedFrames(&unwindCursor, callstack, recorder, stackBase, stackEnd);

    callstack.Add(FrameStore::SentinelFrameIP);
    // Already recorded state in recorder
    return callstack.Size();
}
