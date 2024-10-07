// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "CrashReporting.h"

#include "unknwn.h"
#include "FfiHelper.h"
#include "ScopeFinalizer.h"
#include <shared/src/native-src/util.h>
#include <thread>

extern "C"
{
#include "datadog/common.h"
#include "datadog/profiling.h"
#include "datadog/crashtracker.h"
}

extern "C" IUnknown * STDMETHODCALLTYPE CreateCrashReport(int32_t pid)
{
    auto instance = CrashReporting::Create(pid);
    instance->AddRef();
    return (IUnknown*)instance;
}

CrashReporting::CrashReporting(int32_t pid)
    : _pid(pid)
{
}

CrashReporting::~CrashReporting()
{
    if (_error)
    {
        ddog_Error_drop(&_error.value());
    }

    ddog_crasht_CrashInfo_drop(&_crashInfo);
}

int32_t CrashReporting::Initialize()
{
    auto crashInfoResult = ddog_crasht_CrashInfo_new();

    if (crashInfoResult.tag == DDOG_CRASHT_CRASH_INFO_NEW_RESULT_ERR)
    {
        SetLastError(crashInfoResult.err);
        return 1;
    }

    _crashInfo = crashInfoResult.ok;

    auto result = ddog_crasht_CrashInfo_set_timestamp_to_now(&_crashInfo);

    if (result.tag == DDOG_CRASHT_RESULT_ERR)
    {
        SetLastError(result.err);
        return 1;
    }

    return AddTag("severity", "crash");
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
        delete(this);
    }

    return newCount;
}

int32_t CrashReporting::AddTag(const char* key, const char* value)
{
    auto result = ddog_crasht_CrashInfo_add_tag(&_crashInfo, libdatadog::to_char_slice(key), libdatadog::to_char_slice(value));

    if (result.tag == DDOG_CRASHT_RESULT_ERR)
    {
        SetLastError(result.err);
        return 1;
    }

    return 0;
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

    ddog_crasht_CrashInfo_set_siginfo(&_crashInfo, { (uint64_t)signal, libdatadog::to_char_slice(signalInfo) });

    return 0;
}

int32_t CrashReporting::ResolveStacks(int32_t crashingThreadId, ResolveManagedCallstack resolveCallback, void* context, bool* isSuspicious)
{
    auto threads = GetThreads();

    int32_t successfulThreads = 0;

    *isSuspicious = false;

    for (auto const& [threadId, threadName] : threads)
    {
        auto frames = GetThreadFrames(threadId, resolveCallback, context);

        ddog_crasht_Slice_StackFrame stackTrace{};

        auto count = frames.size();

        stackTrace.len = count;

        auto stackFrames = std::make_unique<ddog_crasht_StackFrame[]>(count);
        auto stackFrameNames = std::make_unique<ddog_crasht_StackFrameNames[]>(count);
        auto strings = std::make_unique<std::string[]>(count);

        stackTrace.ptr = stackFrames.get();

        auto currentIsCrashingThread = threadId == crashingThreadId;
        for (int i = 0; i < count; i++)
        {
            auto const& frame = frames[i];

            if (currentIsCrashingThread)
            {
                // Mark the callstack as suspicious if one of the frames is suspicious
                // or the thread name begins with DD_
                if (frame.isSuspicious || threadName.rfind("DD_", 0) == 0)
                {
                    *isSuspicious = true;
                }
            }

            strings[i] = frame.method;

            stackFrameNames[i] = ddog_crasht_StackFrameNames{
                .colno = { DDOG_OPTION_U32_NONE_U32, 0},
                .filename = {nullptr, 0},
                .lineno = { DDOG_OPTION_U32_NONE_U32, 0},
                .name = libdatadog::to_char_slice(strings[i])
            };

            auto ip = static_cast<uintptr_t>(frame.ip);
            auto sp = static_cast<uintptr_t>(frame.sp);
            auto moduleAddress = static_cast<uintptr_t>(frame.moduleAddress);
            auto symbolAddress = static_cast<uintptr_t>(frame.symbolAddress);

            stackFrames[i] = ddog_crasht_StackFrame{
                .ip = ip,
                .module_base_address = moduleAddress,
                .names{
                    .ptr = &stackFrameNames[i],
                    .len = 1,
                },
                .sp = sp,
                .symbol_address = symbolAddress,
            };
        }

        auto threadIdStr = std::to_string(threadId);

        auto result = ddog_crasht_CrashInfo_set_stacktrace(&_crashInfo, { threadIdStr.c_str(), threadIdStr.length() }, stackTrace);

        if (result.tag == DDOG_CRASHT_RESULT_ERR)
        {
            SetLastError(result.err);
            continue;
        }

        successfulThreads++;

        if (currentIsCrashingThread)
        {
            // Setting the default stacktrace
            result = ddog_crasht_CrashInfo_set_stacktrace(&_crashInfo, { nullptr, 0 }, stackTrace);

            if (result.tag == DDOG_CRASHT_RESULT_ERR)
            {
                SetLastError(result.err);
                continue;
            }
        }
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
        .library_name = libdatadog::to_char_slice(libraryName) ,
        .library_version = libdatadog::to_char_slice(libraryVersion),
        .family = libdatadog::to_char_slice(family),
        .tags = &vecTags
    };

    for (int32_t i = 0; i < tagCount; i++)
    {
        auto tag = tags[i];
        ddog_Vec_Tag_push(&vecTags, libdatadog::to_char_slice(tag.key), libdatadog::to_char_slice(tag.value));
    }

    auto result = ddog_crasht_CrashInfo_set_metadata(&_crashInfo, metadata);

    ddog_Vec_Tag_drop(vecTags);

    if (result.tag == DDOG_CRASHT_RESULT_ERR)
    {
        SetLastError(result.err);
        return 1;
    }

    return 0;
}

int32_t CrashReporting::Send()
{
    return ExportImpl(nullptr);
}

int32_t CrashReporting::WriteToFile(const char* url)
{
    auto endpoint = ddog_endpoint_from_url(libdatadog::to_char_slice(url));
    return ExportImpl(endpoint);
}

int32_t CrashReporting::ExportImpl(ddog_Endpoint* endpoint)
{
    auto result = ddog_crasht_CrashInfo_upload_to_endpoint(&_crashInfo, endpoint);

    on_leave { if (endpoint != nullptr) ddog_endpoint_drop(endpoint); };

    if (result.tag == DDOG_CRASHT_RESULT_ERR)
    {
        SetLastError(result.err);
        return 1;
    }

    return 0;
}

std::vector<StackFrame> CrashReporting::MergeFrames(const std::vector<StackFrame>& nativeFrames, const std::vector<StackFrame>& managedFrames)
{
    std::vector<StackFrame> result;
    result.reserve(std::max(nativeFrames.size(), managedFrames.size()));

    size_t i = 0, j = 0;
    while (i < nativeFrames.size() && j < managedFrames.size())
    {
        if (nativeFrames[i].sp < managedFrames[j].sp)
        {
            result.push_back(nativeFrames[i]);
            ++i;
        }
        else if (managedFrames[j].sp < nativeFrames[i].sp)
        {
            result.push_back(managedFrames[j]);
            ++j;
        }
        else
        {   // frames[i].sp == managedFrames[j].sp
            // Prefer managedFrame when sp values are the same
            result.push_back(managedFrames[j]);
            ++i;
            ++j;
        }
    }

    // Add any remaining frames that are left in either vector
    while (i < nativeFrames.size())
    {
        result.push_back(nativeFrames[i]);
        ++i;
    }

    while (j < managedFrames.size())
    {
        result.push_back(managedFrames[j]);
        ++j;
    }

    return result;
}

int32_t CrashReporting::CrashProcess()
{
    std::thread crashThread([]()
    {
        throw 42;
    });

    crashThread.join();
    return 0;  // If we get there, somehow we failed to crash. Are we even able to do *anything* properly? ;_;
}