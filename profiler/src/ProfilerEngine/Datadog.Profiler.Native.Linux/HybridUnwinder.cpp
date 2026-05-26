// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "HybridUnwinder.h"

#include "Callstack.h"
#include "ManagedCodeCache.h"
#include "StackDeltaMap.h"
#include "StackDeltaTypes.h"

#include "UnwindingRecorder.h"

#include "FrameStore.h"

#include <cstring>
#include <ucontext.h>

#ifndef ARM64
#error "HybridUnwinder is only supported on aarch64"
#endif

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

// Resolve a register value from the virtual register ID.
static inline uintptr_t ResolveReg(UnwindReg reg, uintptr_t sp, uintptr_t fp, uintptr_t lr)
{
    switch (reg)
    {
        case UnwindReg::Sp: return sp;
        case UnwindReg::Fp: return fp;
        case UnwindReg::Lr: return lr;
        default: return 0;
    }
}

HybridUnwinder::HybridUnwinder(ManagedCodeCache* managedCodeCache,
                               const std::atomic<StackDeltaMap*>* deltaMapAtomicPtr) :
    _codeCache(managedCodeCache),
    _deltaMapAtomicPtr(deltaMapAtomicPtr)
{
}

bool HybridUnwinder::UnwindNativeFrames(void* ctx, Callstack& callstack,
    UnwindingRecorder* recorder, uintptr_t& outIp, uintptr_t& outFp) const
{
    auto* uctx = reinterpret_cast<ucontext_t*>(ctx);

    const StackDeltaMap* deltaMap = nullptr;
    if (_deltaMapAtomicPtr != nullptr)
        deltaMap = _deltaMapAtomicPtr->load(std::memory_order_acquire);

    if (deltaMap == nullptr || deltaMap->IsEmpty())
        return false;

    uintptr_t pc = uctx->uc_mcontext.pc;
    uintptr_t sp = uctx->uc_mcontext.sp;
    uintptr_t fp = uctx->uc_mcontext.regs[29]; // x29
    uintptr_t lr = uctx->uc_mcontext.regs[30]; // x30

    static constexpr int MaxNativeFrames = 128;
    for (int i = 0; i < MaxNativeFrames; ++i)
    {
        if (pc == 0)
        {
            if (recorder)
                recorder->RecordFinish(0, FinishReason::FailedGetReg);
            return false;
        }

        auto isManaged = _codeCache->IsManaged(pc);
        if (isManaged.has_value())
        {
            if (isManaged.value())
            {
                if (recorder)
                    recorder->Record(EventType::ManagedTransition, pc, fp);
                outIp = pc;
                outFp = fp;
                return true;
            }
        }
        else
        {
            callstack.Add(FrameStore::UnknownFrameTypeIP);
            if (recorder)
                recorder->RecordFinish(static_cast<std::int32_t>(callstack.Size()), FinishReason::FailedIsManaged);
            return false;
        }

        if (recorder)
            recorder->Record(EventType::NativeFrame, pc, fp, sp);

        if (!callstack.Add(pc))
        {
            if (recorder)
                recorder->RecordFinish(static_cast<std::int32_t>(callstack.Size()), FinishReason::BufferFull);
            return false;
        }

        const UnwindInfo* info = deltaMap->Lookup(pc);
        if (info == nullptr)
        {
            if (recorder)
                recorder->RecordFinish(static_cast<std::int32_t>(callstack.Size()), FinishReason::FailedLibunwindStep);
            return false;
        }

        if (info->IsCommand())
        {
            switch (info->GetCommand())
            {
                case UnwindCommand::Stop:
                    if (recorder)
                        recorder->RecordFinish(static_cast<std::int32_t>(callstack.Size()), FinishReason::Success);
                    return false;

                case UnwindCommand::FramePointer:
                {
                    // Standard FP frame: [fp] = prev_fp, [fp+8] = saved_lr
                    uintptr_t newFp = *reinterpret_cast<uintptr_t*>(fp);
                    uintptr_t newPc = *reinterpret_cast<uintptr_t*>(fp + sizeof(void*));
                    sp = fp + 2 * sizeof(void*);
                    lr = 0;
                    fp = newFp;
                    pc = newPc;
                    continue;
                }

                case UnwindCommand::Signal:
                {
                    // Signal trampoline: the sigcontext is on the stack.
                    // On aarch64: rt_sigframe at sp, sigcontext at sp + 128 + 176 + 8 = sp + 312
                    // regs[0..30] at sigcontext, PC at sigcontext+32*8=sigcontext+256
                    auto* sigRegs = reinterpret_cast<uintptr_t*>(sp + 312);
                    fp = sigRegs[29];
                    lr = sigRegs[30];
                    sp = sigRegs[31];
                    pc = sigRegs[32]; // saved PC in sigcontext
                    continue;
                }

                case UnwindCommand::Invalid:
                default:
                    if (recorder)
                        recorder->RecordFinish(static_cast<std::int32_t>(callstack.Size()), FinishReason::FailedLibunwindStep);
                    return false;
            }
        }

        // General unwind expression: CFA = baseReg + param
        uintptr_t cfa = ResolveReg(info->baseReg, sp, fp, lr) + info->param;

        uintptr_t newPc;
        if (info->auxBaseReg == UnwindReg::Lr)
        {
            // RA is still in LR (leaf function)
            newPc = lr;
        }
        else
        {
            // RA is saved on stack at auxBaseReg + auxParam
            uintptr_t raAddr = ResolveReg(info->auxBaseReg, sp, fp, lr) + info->auxParam;
            newPc = *reinterpret_cast<uintptr_t*>(raAddr);
        }

        // TODO: recover FP from stack if the unwind info indicates it was saved
        // For now, preserve current FP (works for most ARM64 code where FP is
        // preserved across calls).
        uintptr_t newFp = fp;

        sp = cfa;
        fp = newFp;
        lr = 0; // LR is only valid for the topmost frame
        pc = newPc;
    }

    if (recorder)
        recorder->RecordFinish(static_cast<std::int32_t>(callstack.Size()), FinishReason::TooManyNativeFrames);
    return false;
}


void HybridUnwinder::UnwindManagedFrames(uintptr_t ip, uintptr_t fp,
    Callstack& callstack, UnwindingRecorder* recorder,
    std::uintptr_t stackBase, std::uintptr_t stackEnd) const
{
    if (ip == 0)
    {
        if (recorder)
            recorder->RecordFinish(0, FinishReason::FailedGetReg);
        return;
    }

    if (!callstack.Add(ip))
    {
        if (recorder)
            recorder->RecordFinish(static_cast<std::int32_t>(callstack.Size()), FinishReason::BufferFull);
        return;
    }

    if (!IsValidFp(fp, 0, stackBase, stackEnd))
    {
        if (recorder)
            recorder->RecordFinish(0, FinishReason::InvalidFp);
        return;
    }

    uintptr_t prevFp = 0;
    int consecutiveNativeFrames = 0;
    FinishReason finishReason = FinishReason::Success;
    while (true)
    {
        ip = *reinterpret_cast<uintptr_t*>(fp + sizeof(void*));
        if (ip == 0)
            break;

        if (recorder) recorder->Record(EventType::FrameChainStep, ip, fp);

        auto isManaged = _codeCache->IsManaged(ip);
        if (isManaged.has_value() && isManaged.value())
        {
            if (!callstack.Add(ip))
            {
                if (recorder)
                    recorder->RecordFinish(static_cast<std::int32_t>(callstack.Size()), FinishReason::BufferFull);
                break;
            }
            consecutiveNativeFrames = 0;
        }
        else
        {
            static constexpr std::size_t MaxConsecutiveNativeFrames = 8;
            if (!isManaged.has_value())
            {
                if (!callstack.Add(FrameStore::FakeUnknownIP))
                {
                    if (recorder)
                        recorder->RecordFinish(static_cast<std::int32_t>(callstack.Size()), FinishReason::BufferFull);
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
        recorder->RecordFinish(static_cast<std::int32_t>(callstack.Size()), finishReason);
}

std::int32_t HybridUnwinder::Unwind(void* ctx, Callstack& callstack,
                                    uintptr_t stackBase, uintptr_t stackEnd,
                                    UnwindingRecorder* recorder) const
{
    if (recorder)
        recorder->RecordStart(reinterpret_cast<ucontext_t*>(ctx));

    if (stackBase == 0 || stackEnd == 0)
    {
        if (recorder)
            recorder->RecordFinish(0, FinishReason::NoStackBounds);
        return 0;
    }

    ucontext_t localContext;
    if (ctx == nullptr)
    {
        std::memset(&localContext, 0, sizeof(localContext));

        localContext.uc_mcontext.regs[29] = reinterpret_cast<uintptr_t>(__builtin_frame_address(0));
        localContext.uc_mcontext.regs[30] = reinterpret_cast<uintptr_t>(__builtin_return_address(0));
        localContext.uc_mcontext.pc = localContext.uc_mcontext.regs[30];

        uintptr_t sp;
        __asm__ volatile("mov %0, sp" : "=r"(sp));
        localContext.uc_mcontext.sp = sp;

        ctx = &localContext;
    }

    // === Phase 1: Walk native frames using stack deltas (signal-safe, no locks) ===
    uintptr_t managedIp = 0;
    uintptr_t managedFp = 0;
    bool reachedManaged = UnwindNativeFrames(ctx, callstack, recorder, managedIp, managedFp);

    if (!reachedManaged)
        return callstack.Size();

    // === Phase 2: Walk managed frames using the FP chain ===
    UnwindManagedFrames(managedIp, managedFp, callstack, recorder, stackBase, stackEnd);

    return callstack.Size();
}
