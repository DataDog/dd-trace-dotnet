// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "CrashReporting.h"

#include "FfiHelper.h"
#include "ScopeFinalizer.h"
#include "unknwn.h"
#include <shared/src/native-src/string.h>
#include <shared/src/native-src/util.h>
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

extern "C" IUnknown* STDMETHODCALLTYPE CreateCrashReport(int32_t pid)
{
    auto instance = CrashReporting::Create(pid);
    instance->AddRef();
    return (IUnknown*)instance;
}
template <typename T>
struct always_false : std::false_type {};

template <typename T>
bool Succeeded(T value)
{
    if constexpr(std::is_same_v<T, ddog_VoidResult_Tag>)
    {
        return value == DDOG_VOID_RESULT_OK;
    }
    else if constexpr(std::is_same_v<T, ddog_crasht_Result_HandleCrashInfoBuilder_Tag>)
    {
        return value == DDOG_CRASHT_RESULT_HANDLE_CRASH_INFO_BUILDER_OK_HANDLE_CRASH_INFO_BUILDER;
    }
    else if constexpr(std::is_same_v<T, ddog_crasht_Result_HandleStackFrame_Tag>)
    {
        return value == DDOG_CRASHT_RESULT_HANDLE_STACK_FRAME_OK_HANDLE_STACK_FRAME;
    }
    else if constexpr (std::is_same_v<T, ddog_crasht_Result_HandleStackTrace_Tag>)
    {
        return value == DDOG_CRASHT_RESULT_HANDLE_STACK_TRACE_OK_HANDLE_STACK_TRACE;
    }
    else if constexpr (std::is_same_v<T, ddog_crasht_Result_HandleCrashInfo_Tag>)
    {
        return value == DDOG_CRASHT_RESULT_HANDLE_CRASH_INFO_OK_HANDLE_CRASH_INFO;
    }
    else
    {
        static_assert(always_false<T>::value, "unknwn type");
    }
    return false;
}

template <typename T>
std::pair<decltype(T::ok), bool> CrashReporting::ExtractResult(T v)
{
    if (!Succeeded(v.tag)){
        SetLastError(v.err);
        return {{}, false};
    }

    return {v.ok, true};
}

#define CHECK_RESULT(stmt)    \
    do                                \
    {                                 \
        auto result = stmt;           \
        if (!Succeeded(result.tag))     \
        {                             \
            SetLastError(result.err); \
            return 1;                 \
        }                             \
    } while (0);


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

    if (_builder.inner != NULL)
    {
        ddog_crasht_CrashInfoBuilder_drop(&_builder);
    }
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

    // TODO
    // Do we can SetSignal from Windows
    // Maybe call this from linux and windows custom reporter
    CHECK_RESULT(ddog_crasht_CrashInfoBuilder_with_kind(&_builder, DDOG_CRASHT_ERROR_KIND_UNIX_SIGNAL));

    // TODO create ddog_crasht_SigInfo siginfo and call ddog_crasht_CrashInfoBuilder_with_sig_info
    // For now we do not do it because SigInfo requires much more information we can provide
    // If not available we might panic <= to test
    // ddog_crasht_CrashInfoBuilder_with_sig_info(&_builder, { (uint64_t)signal, libdatadog::to_char_slice(signalInfo) });

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

        auto [stackTrace, succeeded] = ExtractResult(ddog_crasht_StackTrace_new());

        if (!succeeded)
        {
            return 1;
        }

        auto currentIsCrashingThread = threadId == crashingThreadId;
        for (int i = 0; i < frames.size(); i++)
        {
            auto [frame, succeeded] = ExtractResult(ddog_crasht_StackFrame_new());

            if (!succeeded)
            {
                return 1;
            }

            auto const& currentFrame = frames[i];

            if (currentIsCrashingThread)
            {
                // Mark the callstack as suspicious if one of the frames is suspicious
                // or the thread name begins with DD_
                if (currentFrame.isSuspicious || threadName.rfind("DD_", 0) == 0)
                {
                    *isSuspicious = true;
                }
            }

            // TODO see this cannot happen inside libdatadog instead of here
            auto ip = shared::Hex(currentFrame.ip);
            auto sp = shared::Hex(currentFrame.sp);
            auto moduleAddress = shared::Hex(currentFrame.moduleAddress);
            auto relativeAddress = shared::Hex(currentFrame.ip - currentFrame.moduleAddress);
            auto symbolAddress = shared::Hex(currentFrame.symbolAddress);
            CHECK_RESULT(ddog_crasht_StackFrame_with_ip(&frame, libdatadog::to_char_slice(ip)));
            CHECK_RESULT(ddog_crasht_StackFrame_with_sp(&frame, libdatadog::to_char_slice(sp)));
            CHECK_RESULT(ddog_crasht_StackFrame_with_module_base_address(&frame, libdatadog::to_char_slice(moduleAddress)));
            CHECK_RESULT(ddog_crasht_StackFrame_with_relative_address(&frame, libdatadog::to_char_slice(relativeAddress)));

            CHECK_RESULT(ddog_crasht_StackFrame_with_symbol_address(&frame, libdatadog::to_char_slice(symbolAddress)));

            auto buildId = currentFrame.buildId;
            if (buildId.size() != 0)
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

        CHECK_RESULT(ddog_crasht_StackTrace_set_complete(&stackTrace));

        auto threadIdStr = std::to_string(threadId);

        auto thread = ddog_crasht_ThreadData{
            .crashed = currentIsCrashingThread,
            .name = {threadIdStr.data(), threadIdStr.size()},
            .stack = stackTrace,
            .state = {nullptr, 0}};

        CHECK_RESULT(ddog_crasht_CrashInfoBuilder_with_thread(&_builder, thread));

        successfulThreads++;

        if (currentIsCrashingThread)
        {
            CHECK_RESULT(ddog_crasht_CrashInfoBuilder_with_stack(&_builder, &stackTrace));
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
        .library_name = libdatadog::to_char_slice(libraryName),
        .library_version = libdatadog::to_char_slice(libraryVersion),
        .family = libdatadog::to_char_slice(family),
        .tags = &vecTags};

    for (int32_t i = 0; i < tagCount; i++)
    {
        auto tag = tags[i];
        auto result = ddog_Vec_Tag_push(&vecTags, libdatadog::to_char_slice(tag.key), libdatadog::to_char_slice(tag.value));
        if (result.tag != DDOG_VEC_TAG_PUSH_RESULT_OK)
        {
            // Let's not stop right here.
            ddog_Error_drop(&result.err);
        }
    }

    auto result = ddog_Vec_Tag_push(&vecTags, libdatadog::to_char_slice("severity"), libdatadog::to_char_slice("crash"));
    if (result.tag != DDOG_VEC_TAG_PUSH_RESULT_OK)
    {
        // Let's not stop right here.
        ddog_Error_drop(&result.err);
    }
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
    return ExportImpl(endpoint);
}

int32_t CrashReporting::ExportImpl(ddog_Endpoint* endpoint)
{
    auto [crashInfo, succeeded] = ExtractResult(ddog_crasht_CrashInfoBuilder_build(&_builder));
    if (!succeeded)
    {
        return 1;
    }

    on_leave
    {
        if (endpoint != nullptr) ddog_endpoint_drop(endpoint);
    };

    CHECK_RESULT(ddog_crasht_CrashInfo_upload_to_endpoint(&crashInfo, endpoint));

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
        { // frames[i].sp == managedFrames[j].sp
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
    std::thread crashThread([]() {
        throw 42;
    });

    crashThread.join();
    return 0; // If we get there, somehow we failed to crash. Are we even able to do *anything* properly? ;_;
}

static void ToHex(std::ostringstream& oss, std::uint8_t* ptr, std::size_t size)
{
    oss << std::hex << std::setfill('0');

    for (size_t i = 0; i < size; ++i)
    {
        oss << std::setw(2) << static_cast<int>(ptr[i]);
    }
}

#ifdef LINUX
BuildId BuildId::From(const char* path)
{
    if (path == nullptr)
    {
        return {};
    }

    std::size_t size = 0;
    auto ptr = blaze_read_elf_build_id(path, &size);
    if (ptr != nullptr)
    {
        std::ostringstream oss;
        ToHex(oss, ptr, size);
        ::free(ptr);
        return BuildId(std::move(oss.str()));
    }

    return {};
}
#else
BuildId BuildId::From(GUID sig, DWORD age)
{
    std::ostringstream oss;
    ToHex(oss, (uint8_t*)&sig, 16);
    oss << std::hex << age; // age should be in hex too ?
    return BuildId(oss.str());
}
#endif