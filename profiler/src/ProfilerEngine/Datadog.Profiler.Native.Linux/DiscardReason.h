// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

enum class DiscardReason
{
    InSegvHandler = 0,
    InsideWrappedFunction,
    ExternalSignal,
    UnknownThread,
    WrongManagedThread,
    UnsufficientSpace,
    EmptyBacktrace,
    FailedAcquiringLock,
    TimedOut, // This is used when we cannot reserve a buffer in the ring buffer

    // This item must be the last one
    GuardItem
};

// This pragma forces a compilation error if we forgot to add an enum item
#if __clang__
#pragma clang diagnostic push
#pragma clang diagnostic error "-Wswitch"
#else
#pragma warning(error : 4062)
#endif
static const char* to_string(DiscardReason type)
{
    switch (type)
    {
        case DiscardReason::InSegvHandler:
            return "_in_sigsegv_handler";
        case DiscardReason::InsideWrappedFunction:
            return "_inside_wrapped_function";
        case DiscardReason::ExternalSignal:
            return "_external_signal";
        case DiscardReason::UnknownThread:
            return "_unknown_thread";
        case DiscardReason::WrongManagedThread:
            return "_wrong_managed_thread";
        case DiscardReason::UnsufficientSpace:
            return "_unsufficient_space";
        case DiscardReason::EmptyBacktrace:
            return "_empty_backtrace";
        case DiscardReason::FailedAcquiringLock:
            return "_failed_acquiring_lock";
        case DiscardReason::TimedOut:
            return "_timed_out";
        case DiscardReason::GuardItem:
            // pass through
            break;
    }
    return "unknown_discard_type";
}
#if __clang__
#pragma clang diagnostic pop
#else
#pragma warning(default : 4062)
#endif