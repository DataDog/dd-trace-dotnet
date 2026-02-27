// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "CrashReporting.h"

#include "FfiHelper.h"
#include "ScopeFinalizer.h"
#include "unknwn.h"
#include <shared/src/native-src/string.h>
#include <shared/src/native-src/util.h>

#include <algorithm>
#include <thread>

#ifdef _WIN32
#include "Windows.h"
#endif

extern "C"
{
#ifdef LINUX
#include "datadog/blazesym.h"
#endif
#include "datadog/common.h"
#include "datadog/crashtracker.h"
#include "datadog/profiling.h"
}

#include "CrashReportingHelper.hpp"

extern "C" IUnknown* STDMETHODCALLTYPE CreateCrashReport(int32_t pid)
{
    auto instance = CrashReporting::Create(pid);
    instance->AddRef();
    return (IUnknown*)instance;
}

CrashReporting::CrashReporting(int32_t pid) :
    _pid(pid),
    _signal(0),
    _error{std::nullopt},
    _builder{nullptr},
    _refCount(0)
{
}

CrashReporting::~CrashReporting()
{
    if (_error)
    {
        ddog_Error_drop(&_error.value());
    }
}

ddog_crasht_OsInfo GetOsInfo()
{
    auto osType = libdatadog::to_char_slice(
#ifdef _WINDOWS
                "Windows"
#elif MACOS
                "macOS"
#else
                "Linux"
#endif
    );

    auto architecture = libdatadog::to_char_slice(
#ifdef _WINDOWS
#ifdef BIT64
                "x86_64"
#else
                "x86"
#endif
#elif AMD64
                "x86_64"
#elif X86
                "x86"
#elif ARM64
                "arm64"
#elif ARM
                "arm"
#endif
    );

    auto osInfo = ddog_crasht_OsInfo {
        .architecture = architecture,
        .bitness = {},
        .os_type = osType,
        .version = {}
    };

    return osInfo;
}

int32_t CrashReporting::Initialize()
{
    bool succeeded = false;
    std::tie(_builder, succeeded) = ExtractResult(ddog_crasht_CrashInfoBuilder_new());
    if (!succeeded)
    {
        return 1;
    }

    CHECK_RESULT(ddog_crasht_CrashInfoBuilder_with_timestamp_now(&_builder));

    CHECK_RESULT(ddog_crasht_CrashInfoBuilder_with_proc_info(&_builder, {_pid}));

    CHECK_RESULT(ddog_crasht_CrashInfoBuilder_with_os_info(&_builder, GetOsInfo()));

#ifdef _WINDOWS
    CHECK_RESULT(ddog_crasht_CrashInfoBuilder_with_kind(&_builder, DDOG_CRASHT_ERROR_KIND_UNHANDLED_EXCEPTION));
#else
    CHECK_RESULT(ddog_crasht_CrashInfoBuilder_with_kind(&_builder, DDOG_CRASHT_ERROR_KIND_UNIX_SIGNAL));
#endif

    return 0;
}

void CrashReporting::SetLastError(ddog_Error error)
{
    if (_error)
    {
        ddog_Error_drop(&_error.value());
    }

    _error.emplace(error);
}

int32_t CrashReporting::GetLastError(const char** message, int* length)
{
    if (_error)
    {
        auto result = ddog_Error_message(&_error.value());

        *message = result.ptr;
        *length = result.len;
    }

    return 0;
}

HRESULT CrashReporting::QueryInterface(REFIID riid, void** ppvObject)
{
    if (riid == __uuidof(IUnknown) || riid == __uuidof(ICrashReporting))
    {
        *ppvObject = (ICrashReporting*)this;
        AddRef();
        return S_OK;
    }

    return E_NOINTERFACE;
}

ULONG CrashReporting::AddRef()
{
    return ++_refCount;
}

ULONG CrashReporting::Release()
{
    auto newCount = --_refCount;

    if (newCount == 0)
    {
        delete (this);
    }

    return newCount;
}

// Used only to crash in tests
int32_t CrashReporting::Panic()
{
    // The goal here (like with CrashProcess), is to crash the app in the test
    // Here we want the crash to happen in Rust (panic)
    auto faultySlice = ddog_CharSlice{ .ptr = (const char*)0x1, .len = 10 };
    ddog_Vec_Tag_push((ddog_Vec_Tag*)0x1, faultySlice, faultySlice);

    return 1;
}

ddog_crasht_SignalNames GetSignal(int signal){
    switch (signal) {
        case 6: return DDOG_CRASHT_SIGNAL_NAMES_SIGABRT;
        case 7: return DDOG_CRASHT_SIGNAL_NAMES_SIGBUS;
        case 11: return DDOG_CRASHT_SIGNAL_NAMES_SIGSEGV;
        case 31: return DDOG_CRASHT_SIGNAL_NAMES_SIGSYS;
        default:
            return DDOG_CRASHT_SIGNAL_NAMES_UNKNOWN;
    }
}

int32_t CrashReporting::SetSignalInfo(int32_t signal, const char* description)
{
    std::string signalInfo;

    if (description == nullptr)
    {
        // If no signal description is provided, try to populate it
        signalInfo = GetSignalInfo(signal);
    }
    else
    {
        signalInfo = std::string(description);
    }

    auto siginfo = ddog_crasht_SigInfo {
        .addr = {nullptr, 0},
        .code = 0,
        .code_human_readable = DDOG_CRASHT_SI_CODES_UNKNOWN,
        .signo = signal,
        .signo_human_readable = GetSignal(signal),
    };
    CHECK_RESULT(ddog_crasht_CrashInfoBuilder_with_sig_info(&_builder, siginfo));

    return 0;
}

int32_t CrashReporting::ResolveStacks(int32_t crashingThreadId, void* crashingThreadContext, ResolveManagedCallstack resolveCallback, void* context, bool* isSuspicious)
{
    auto threads = GetThreads();

    int32_t successfulThreads = 0;

    *isSuspicious = false;

    for (auto const& [threadId, threadName] : threads)
    {
        auto currentIsCrashingThread = threadId == crashingThreadId;
        auto threadContext = currentIsCrashingThread ? crashingThreadContext : nullptr;
        auto frames = GetThreadFrames(threadId, threadContext, resolveCallback, context);

        auto [stackTrace, succeeded] = ExtractResult(ddog_crasht_StackTrace_new());

        if (!succeeded)
        {
            return 1;
        }

        // GetThreadFrames returns the frames in reverse order, so we need to iterate in reverse
        for (auto it = frames.rbegin(); it != frames.rend(); it++)
        {
            auto [frame, succeeded] = ExtractResult(ddog_crasht_StackFrame_new());

            if (!succeeded)
            {
                return 1;
            }

            auto const& currentFrame = *it;

            if (currentIsCrashingThread)
            {
                // Mark the callstack as suspicious if one of the frames is suspicious
                // or the thread name begins with DD_
                if (currentFrame.isSuspicious || threadName.rfind("DD_", 0) == 0)
                {
                    *isSuspicious = true;
                }
            }

            auto relativeAddress = currentFrame.ip - currentFrame.moduleAddress;
            CHECK_RESULT(ddog_crasht_StackFrame_with_ip(&frame, currentFrame.ip));
            CHECK_RESULT(ddog_crasht_StackFrame_with_sp(&frame, currentFrame.sp));
            CHECK_RESULT(ddog_crasht_StackFrame_with_module_base_address(&frame, currentFrame.moduleAddress));
            CHECK_RESULT(ddog_crasht_StackFrame_with_relative_address(&frame, relativeAddress));
            CHECK_RESULT(ddog_crasht_StackFrame_with_symbol_address(&frame, currentFrame.symbolAddress));

            if (!currentFrame.modulePath.empty())
            {
                CHECK_RESULT(ddog_crasht_StackFrame_with_path(&frame, {currentFrame.modulePath.data(), currentFrame.modulePath.size()}));
            }

            auto buildId = currentFrame.buildId;
            if (!buildId.empty())
            {
                CHECK_RESULT(ddog_crasht_StackFrame_with_build_id(&frame, {buildId.data(), buildId.size()}));
#ifdef _WINDOWS
                CHECK_RESULT(ddog_crasht_StackFrame_with_build_id_type(&frame, DDOG_CRASHT_BUILD_ID_TYPE_PDB));
#else
                CHECK_RESULT(ddog_crasht_StackFrame_with_build_id_type(&frame, DDOG_CRASHT_BUILD_ID_TYPE_GNU));
#endif
            }
            CHECK_RESULT(ddog_crasht_StackFrame_with_function(&frame, libdatadog::to_char_slice(currentFrame.method)));
            CHECK_RESULT(ddog_crasht_StackTrace_push_frame(&stackTrace, &frame, /*is incomplete*/ true));
        }

        // Only mark the stack trace as complete if we actually captured frames
        if (!frames.empty())
        {
            CHECK_RESULT(ddog_crasht_StackTrace_set_complete(&stackTrace));
        }

        auto threadIdStr = std::to_string(threadId);
        // stackTrace is consumed by the API, meaning that we *MUST* not use this handle
        auto thread = ddog_crasht_ThreadData{
            .crashed = currentIsCrashingThread,
            .name = {threadIdStr.data(), threadIdStr.size()},
            .stack = stackTrace,
            .state = {nullptr, 0}
        };

        CHECK_RESULT(ddog_crasht_CrashInfoBuilder_with_thread(&_builder, thread));
        successfulThreads++;
    }

    if (successfulThreads != threads.size())
    {
        return 1;
    }

    return 0;
}

int32_t CrashReporting::SetMetadata(const char* libraryName, const char* libraryVersion, const char* family, Tag* tags, int32_t tagCount)
{
    auto vecTags = ddog_Vec_Tag_new();

    const ddog_crasht_Metadata metadata = {
        .library_name = libdatadog::to_char_slice(libraryName),
        .library_version = libdatadog::to_char_slice(libraryVersion),
        .family = libdatadog::to_char_slice(family),
        .tags = &vecTags};

    for (int32_t i = 0; i < tagCount; i++)
    {
        auto const& tag = tags[i];
        CHECK_RESULT(ddog_Vec_Tag_push(&vecTags, libdatadog::to_char_slice(tag.key), libdatadog::to_char_slice(tag.value)));
    }

    CHECK_RESULT(ddog_Vec_Tag_push(&vecTags, libdatadog::to_char_slice("severity"), libdatadog::to_char_slice("crash")));

    CHECK_RESULT(ddog_crasht_CrashInfoBuilder_with_metadata(&_builder, metadata));

    ddog_Vec_Tag_drop(vecTags);

    return 0;
}

int32_t CrashReporting::Send()
{
    return ExportImpl(nullptr);
}

int32_t CrashReporting::WriteToFile(const char* url)
{
    auto endpoint = ddog_endpoint_from_url(libdatadog::to_char_slice(url));
    on_leave { if (endpoint != nullptr) ddog_endpoint_drop(endpoint); };
    return ExportImpl(endpoint);
}

int32_t CrashReporting::ExportImpl(ddog_Endpoint* endpoint)
{
    // _builder.inner will be claimed by the Rust API. No need to call XX_drop.
    auto [crashInfo, succeeded] = ExtractResult(ddog_crasht_CrashInfoBuilder_build(&_builder));

    if (!succeeded)
    {
        return 1;
    }

    CHECK_RESULT(ddog_crasht_CrashInfo_upload_to_endpoint(&crashInfo, endpoint));

    return 0;
}

std::vector<StackFrame> CrashReporting::MergeFrames(const std::vector<StackFrame>& nativeFrames, const std::vector<StackFrame>& managedFrames)
{
    std::vector<StackFrame> result;
    // it's safe here to not use nativeFrames.size() + managedFrames.size()
    // because the managed frames should be a subset of the native frames
    result.reserve((std::max)(nativeFrames.size(), managedFrames.size()));

    auto nativeIt = nativeFrames.rbegin();
    auto managedIt = managedFrames.rbegin();
    while (nativeIt != nativeFrames.rend() && managedIt != managedFrames.rend())
    {
        if (nativeIt->sp > managedIt->sp)
        {
            result.push_back(*nativeIt);
            ++nativeIt;
        }
        else if (managedIt->sp > nativeIt->sp)
        {
            result.push_back(*managedIt);
            ++managedIt;
        }
        else
        { // frames[i].sp == managedFrames[j].sp
            // Prefer managedFrame when sp values are the same
            result.push_back(*managedIt);
            ++nativeIt;
            ++managedIt;
        }
    }

    // Add any remaining frames that are left in either vector
    while (nativeIt != nativeFrames.rend())
    {
        result.push_back(*nativeIt);
        ++nativeIt;
    }

    while (managedIt != managedFrames.rend())
    {
        result.push_back(*managedIt);
        ++managedIt;
    }

    return result;
}

int32_t CrashReporting::CrashProcess()
{
    std::thread crashThread([]() {
        throw 42;
    });

    crashThread.join();
    return 0; // If we get there, somehow we failed to crash. Are we even able to do *anything* properly? ;_;
}

#ifdef LINUX
BuildId BuildId::From(const char* path)
{
    std::size_t size = 0;
    auto ptr = std::unique_ptr<uint8_t[], decltype(&::free)>(blaze_read_elf_build_id(path, &size), ::free);
    if (ptr == nullptr)
    {
        return {};
    }

    std::ostringstream oss;
    oss << std::hex << std::setfill('0');

    for (size_t i = 0; i < size; ++i)
    {
        oss << std::setw(2) << static_cast<int>(ptr[i]);
    }
    return BuildId(oss.str());
}
#else
BuildId BuildId::From(GUID sig, DWORD age)
{
    std::ostringstream oss;
    oss << shared::Hex(sig.Data1, 8, "")
        << shared::Hex(sig.Data2, 4, "")
        << shared::Hex(sig.Data3, 4, "")
        << shared::Hex(sig.Data4[0], 2, "") << shared::Hex(sig.Data4[1], 2, "")
        << shared::Hex(sig.Data4[2], 2, "") << shared::Hex(sig.Data4[3], 2, "") << shared::Hex(sig.Data4[4], 2, "")
        << shared::Hex(sig.Data4[5], 2, "") << shared::Hex(sig.Data4[6], 2, "") << shared::Hex(sig.Data4[7], 2, "");
    oss << std::hex << age; // age should be in hex too ?
    return BuildId(oss.str());
}
#endif