// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#ifdef ARM64

#include "gtest/gtest.h"

#include "Callstack.h"
#include "HybridUnwinder.h"
#include "ManagedCodeCache.h"
#include "MetricsRegistry.h"

#include <atomic>
#include <cstddef>
#include <cstdint>
#include <pthread.h>
#include <signal.h>
#include <thread>
#include <tuple>
#include <vector>

namespace {

struct SpinControl
{
    std::atomic<std::uint32_t> spinning{0};
    std::atomic<std::uint32_t> release{0};
};

static_assert(offsetof(SpinControl, release) == 4);

constexpr std::uintptr_t PoisonFp = 0xffffff80a2419420;

}

extern "C" void hybrid_unwinder_test_poisoned_frame(SpinControl* control, std::uintptr_t poisonFp);
extern "C" void hybrid_unwinder_test_fp_based_frame(SpinControl* control, std::uintptr_t poisonFp);
extern "C" void hybrid_unwinder_test_sane_frame(SpinControl* control, std::uintptr_t poisonFp);
extern "C" char hybrid_unwinder_test_spin_without_cfi[];
extern "C" char hybrid_unwinder_test_spin_without_cfi_end[];
extern "C" char hybrid_unwinder_test_gcc_style_frame_return[];
extern "C" char hybrid_unwinder_test_poisoned_frame_return[];
extern "C" char hybrid_unwinder_test_stale_fp_slot[];
extern "C" char hybrid_unwinder_test_stale_fp_slot_end[];
extern "C" char hybrid_unwinder_test_fp_based_frame_return[];
extern "C" char hybrid_unwinder_test_spin_with_cfi[];
extern "C" char hybrid_unwinder_test_spin_with_cfi_end[];
extern "C" char hybrid_unwinder_test_sane_frame_return[];

asm(R"(
    .text

    .p2align 2
    .globl hybrid_unwinder_test_spin_without_cfi
    .type hybrid_unwinder_test_spin_without_cfi, %function
hybrid_unwinder_test_spin_without_cfi:
    stp x29, x30, [sp, #-16]!
    mov x29, sp
    mov w1, #1
    stlr w1, [x0]
    add x2, x0, #4
1:
    ldar w1, [x2]
    cbz w1, 1b
    ldp x29, x30, [sp], #16
    ret
    .size hybrid_unwinder_test_spin_without_cfi, .-hybrid_unwinder_test_spin_without_cfi
    .globl hybrid_unwinder_test_spin_without_cfi_end
hybrid_unwinder_test_spin_without_cfi_end:

    .p2align 2
    .globl hybrid_unwinder_test_gcc_style_frame
    .type hybrid_unwinder_test_gcc_style_frame, %function
hybrid_unwinder_test_gcc_style_frame:
    .cfi_startproc
    sub sp, sp, #480
    .cfi_def_cfa_offset 480
    stp x29, x30, [sp, #384]
    .cfi_offset 29, -96
    .cfi_offset 30, -88
    add x29, sp, #384
    bl hybrid_unwinder_test_spin_without_cfi
    .globl hybrid_unwinder_test_gcc_style_frame_return
hybrid_unwinder_test_gcc_style_frame_return:
    ldp x29, x30, [sp, #384]
    add sp, sp, #480
    .cfi_restore 29
    .cfi_restore 30
    .cfi_def_cfa_offset 0
    ret
    .cfi_endproc
    .size hybrid_unwinder_test_gcc_style_frame, .-hybrid_unwinder_test_gcc_style_frame

    .p2align 2
    .globl hybrid_unwinder_test_poisoned_frame
    .type hybrid_unwinder_test_poisoned_frame, %function
hybrid_unwinder_test_poisoned_frame:
    .cfi_startproc
    stp x29, x30, [sp, #-16]!
    .cfi_def_cfa_offset 16
    .cfi_offset 29, -16
    .cfi_offset 30, -8
    mov x29, sp
    .cfi_def_cfa w29, 16
    sub sp, sp, #1024
    adr x2, hybrid_unwinder_test_poisoned_frame_return
    mov x3, sp
    add x4, sp, #1024
2:
    stp x1, x2, [x3], #16
    cmp x3, x4
    b.lo 2b
    bl hybrid_unwinder_test_gcc_style_frame
    .globl hybrid_unwinder_test_poisoned_frame_return
hybrid_unwinder_test_poisoned_frame_return:
    mov sp, x29
    ldp x29, x30, [sp], #16
    .cfi_def_cfa sp, 0
    .cfi_restore 29
    .cfi_restore 30
    ret
    .cfi_endproc
    .size hybrid_unwinder_test_poisoned_frame, .-hybrid_unwinder_test_poisoned_frame

    .p2align 2
    .globl hybrid_unwinder_test_stale_fp_slot
    .type hybrid_unwinder_test_stale_fp_slot, %function
hybrid_unwinder_test_stale_fp_slot:
    .cfi_startproc
    sub sp, sp, #48
    .cfi_def_cfa_offset 48
    stp x29, x30, [sp, #32]
    add x29, sp, #32
    stp x1, x30, [sp]
    .cfi_offset 29, -48
    .cfi_offset 30, -40
    mov w1, #1
    stlr w1, [x0]
    add x2, x0, #4
4:
    ldar w1, [x2]
    cbz w1, 4b
    ldp x29, x30, [sp, #32]
    add sp, sp, #48
    .cfi_restore 29
    .cfi_restore 30
    .cfi_def_cfa_offset 0
    ret
    .cfi_endproc
    .size hybrid_unwinder_test_stale_fp_slot, .-hybrid_unwinder_test_stale_fp_slot
    .globl hybrid_unwinder_test_stale_fp_slot_end
hybrid_unwinder_test_stale_fp_slot_end:

    .p2align 2
    .globl hybrid_unwinder_test_fp_based_frame
    .type hybrid_unwinder_test_fp_based_frame, %function
hybrid_unwinder_test_fp_based_frame:
    .cfi_startproc
    stp x29, x30, [sp, #-16]!
    .cfi_def_cfa_offset 16
    .cfi_offset 29, -16
    .cfi_offset 30, -8
    mov x29, sp
    .cfi_def_cfa w29, 16
    bl hybrid_unwinder_test_stale_fp_slot
    .globl hybrid_unwinder_test_fp_based_frame_return
hybrid_unwinder_test_fp_based_frame_return:
    ldp x29, x30, [sp], #16
    .cfi_def_cfa sp, 0
    .cfi_restore 29
    .cfi_restore 30
    ret
    .cfi_endproc
    .size hybrid_unwinder_test_fp_based_frame, .-hybrid_unwinder_test_fp_based_frame

    .p2align 2
    .globl hybrid_unwinder_test_spin_with_cfi
    .type hybrid_unwinder_test_spin_with_cfi, %function
hybrid_unwinder_test_spin_with_cfi:
    .cfi_startproc
    stp x29, x30, [sp, #-16]!
    .cfi_def_cfa_offset 16
    .cfi_offset 29, -16
    .cfi_offset 30, -8
    mov x29, sp
    .cfi_def_cfa w29, 16
    mov w1, #1
    stlr w1, [x0]
    add x2, x0, #4
5:
    ldar w1, [x2]
    cbz w1, 5b
    ldp x29, x30, [sp], #16
    .cfi_def_cfa sp, 0
    .cfi_restore 29
    .cfi_restore 30
    ret
    .cfi_endproc
    .size hybrid_unwinder_test_spin_with_cfi, .-hybrid_unwinder_test_spin_with_cfi
    .globl hybrid_unwinder_test_spin_with_cfi_end
hybrid_unwinder_test_spin_with_cfi_end:

    .p2align 2
    .globl hybrid_unwinder_test_sane_frame
    .type hybrid_unwinder_test_sane_frame, %function
hybrid_unwinder_test_sane_frame:
    .cfi_startproc
    stp x29, x30, [sp, #-16]!
    .cfi_def_cfa_offset 16
    .cfi_offset 29, -16
    .cfi_offset 30, -8
    mov x29, sp
    .cfi_def_cfa w29, 16
    bl hybrid_unwinder_test_spin_with_cfi
    .globl hybrid_unwinder_test_sane_frame_return
hybrid_unwinder_test_sane_frame_return:
    ldp x29, x30, [sp], #16
    .cfi_def_cfa sp, 0
    .cfi_restore 29
    .cfi_restore 30
    ret
    .cfi_endproc
    .size hybrid_unwinder_test_sane_frame, .-hybrid_unwinder_test_sane_frame
)");

namespace {

struct UnwindRequest
{
    HybridUnwinder const* unwinder;
    Callstack* callstack;
    std::uintptr_t stackBase = 0;
    std::uintptr_t stackEnd = 0;
    std::atomic<bool> done{false};
};

UnwindRequest* s_unwindRequest = nullptr;

void UnwindFromSignalHandler(int, siginfo_t*, void* context)
{
    auto* request = s_unwindRequest;
    request->unwinder->Unwind(context, *request->callstack, request->stackBase, request->stackEnd);
    request->done.store(true, std::memory_order_release);
}

std::pair<std::uintptr_t, std::uintptr_t> GetCurrentThreadStackBounds()
{
    pthread_attr_t attr;
    pthread_getattr_np(pthread_self(), &attr);
    void* address = nullptr;
    std::size_t size = 0;
    pthread_attr_getstack(&attr, &address, &size);
    pthread_attr_destroy(&attr);
    auto base = reinterpret_cast<std::uintptr_t>(address);
    return {base, base + size};
}

std::vector<std::uintptr_t> UnwindSpinningThread(void (*threadBody)(SpinControl*, std::uintptr_t))
{
    MetricsRegistry metricsRegistry;
    ManagedCodeCache codeCache(nullptr, metricsRegistry);
    HybridUnwinder unwinder(&codeCache);

    std::vector<std::uintptr_t> buffer(Callstack::MaxFrames);
    Callstack callstack(shared::span<std::uintptr_t>(buffer.data(), buffer.size()));

    UnwindRequest request;
    request.unwinder = &unwinder;
    request.callstack = &callstack;
    s_unwindRequest = &request;

    struct sigaction action = {};
    action.sa_sigaction = UnwindFromSignalHandler;
    action.sa_flags = SA_SIGINFO;
    sigemptyset(&action.sa_mask);
    struct sigaction previousAction = {};
    sigaction(SIGUSR2, &action, &previousAction);

    SpinControl control;
    std::thread worker([&request, &control, threadBody]() {
        std::tie(request.stackBase, request.stackEnd) = GetCurrentThreadStackBounds();
        threadBody(&control, PoisonFp);
    });

    while (control.spinning.load(std::memory_order_acquire) == 0)
    {
        std::this_thread::yield();
    }

    EXPECT_EQ(pthread_kill(worker.native_handle(), SIGUSR2), 0);

    while (!request.done.load(std::memory_order_acquire))
    {
        std::this_thread::yield();
    }

    control.release.store(1, std::memory_order_release);
    worker.join();

    sigaction(SIGUSR2, &previousAction, nullptr);
    s_unwindRequest = nullptr;

    buffer.resize(callstack.Size());
    return buffer;
}

}

TEST(HybridUnwinderTest, StopsAtInvalidFramePointerAfterFramePointerFallback)
{
    auto frames = UnwindSpinningThread(hybrid_unwinder_test_poisoned_frame);

    ASSERT_EQ(frames.size(), 3);
    EXPECT_GE(frames[0], reinterpret_cast<std::uintptr_t>(hybrid_unwinder_test_spin_without_cfi));
    EXPECT_LT(frames[0], reinterpret_cast<std::uintptr_t>(hybrid_unwinder_test_spin_without_cfi_end));
    EXPECT_EQ(frames[1], reinterpret_cast<std::uintptr_t>(hybrid_unwinder_test_gcc_style_frame_return));
    EXPECT_EQ(frames[2], reinterpret_cast<std::uintptr_t>(hybrid_unwinder_test_poisoned_frame_return));
}

TEST(HybridUnwinderTest, StopsBeforeSteppingWithFramePointerOutsideStack)
{
    auto frames = UnwindSpinningThread(hybrid_unwinder_test_fp_based_frame);

    ASSERT_EQ(frames.size(), 2);
    EXPECT_GE(frames[0], reinterpret_cast<std::uintptr_t>(hybrid_unwinder_test_stale_fp_slot));
    EXPECT_LT(frames[0], reinterpret_cast<std::uintptr_t>(hybrid_unwinder_test_stale_fp_slot_end));
    EXPECT_EQ(frames[1], reinterpret_cast<std::uintptr_t>(hybrid_unwinder_test_fp_based_frame_return));
}

TEST(HybridUnwinderTest, UnwindsPastWellFormedFrames)
{
    auto frames = UnwindSpinningThread(hybrid_unwinder_test_sane_frame);

    ASSERT_GT(frames.size(), 3);
    EXPECT_GE(frames[0], reinterpret_cast<std::uintptr_t>(hybrid_unwinder_test_spin_with_cfi));
    EXPECT_LT(frames[0], reinterpret_cast<std::uintptr_t>(hybrid_unwinder_test_spin_with_cfi_end));
    EXPECT_EQ(frames[1], reinterpret_cast<std::uintptr_t>(hybrid_unwinder_test_sane_frame_return));
}

#endif
