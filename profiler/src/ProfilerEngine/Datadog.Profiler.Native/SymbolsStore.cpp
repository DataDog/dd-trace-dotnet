// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "SymbolsStore.h"

#include "FfiHelper.h"
#include "SuccessImpl.hpp"

#include "Log.h"

#include <type_traits>

extern "C"
{
#include "datadog/common.h"
#include "datadog/profiling.h"
}

namespace libdatadog {

// make sure that libdatadog internal types are pointers to some object.
// Our wrapping types will just hide those pointers
static_assert(std::is_pointer_v<ddog_prof_StringId2>, "ddog_prof_String2 is expected to be a pointer");
static_assert(std::is_pointer_v<ddog_prof_FunctionId2>, "ddog_prof_FunctionId2 is expected to be a pointer");
static_assert(std::is_pointer_v<ddog_prof_MappingId2>, "ddog_prof_MappingId2 is expected to be a pointer");

struct KnownSymbols
{
    FunctionId* NotResolvedFrameId;
    ModuleId* NotResolvedModuleId;
    ModuleId* UnloadedModuleId;
    ModuleId* UnknownNativeModuleId;

    ModuleId* FakeModuleId;

    FunctionId* FakeContentionFrameId;
    FunctionId* FakeAllocationFrameId;

    FunctionId* FakeFunctionId;
    FunctionId* UnknownNativeFrameId;
    FunctionId* UnknownManagedFrameId;
    StringId* UnknownManagedTypeId;
    ModuleId* UnknownManagedAssemblyId;
    ModuleId* ClrModuleId;
    FunctionId* Gen0FrameId;
    FunctionId* Gen1FrameId;
    FunctionId* Gen2FrameId;
    FunctionId* UnknownGenFrameId;
    FunctionId* GCRootFrameId;
    FunctionId* NativeGCRootFrameId;
    FunctionId* DotNetRootFrameId;
    FunctionId* NativeDotNetRootFrameId;

    StringId* ThreadId;
    StringId* ThreadName;
    StringId* ProcessId;
    StringId* AppDomainName;
    StringId* LocalRootSpanId;
    StringId* SpanId;
    StringId* ExceptionType;
    StringId* ExceptionMessage;
    StringId* AllocationClass;
    StringId* GarbageCollectionGeneration;
    StringId* GarbageCollectionNumber;
    StringId* TimelineEventType;
    StringId* TimelineEventTypeStopTheWorld;
    StringId* TimelineEventTypeGarbageCollection;
    StringId* TimelineEventTypeThreadStart;
    StringId* TimelineEventTypeThreadStop;
    StringId* GarbageCollectionReason;
    StringId* GarbageCollectionType;
    StringId* GarbageCollectionCompacting;
    StringId* ObjectLifetime;
    StringId* ObjectId;
    StringId* ObjectGeneration;
    StringId* RequestUrl;
    StringId* RequestStatusCode;
    StringId* RequestError;
    StringId* RequestRedirectUrl;
    StringId* RequestDnsWait;
    StringId* RequestDnsDuration;
    StringId* RequestDnsSuccess;
    StringId* RequestHandshakeWait;
    StringId* RequestHandshakeDuration;
    StringId* RequestHandshakeError;
    StringId* RequestSocketDuration;
    StringId* RequestDuration;
    StringId* ResponseContentDuration;
    StringId* RequestResponseThreadId;
    StringId* RequestResponseThreadName;
    StringId* GcCpuThread;
    StringId* BucketLabelName;
    StringId* WaitBucketLabelName;
    StringId* RawCountLabelName;
    StringId* RawDurationLabelName;
    StringId* BlockingThreadIdLabelName;
    StringId* BlockingThreadNameLabelName;
    StringId* ContentionTypeLabelName;
    FunctionId* ThreadStartFrame;
    FunctionId* ThreadStopFrame;
    ModuleId* NativeGcModuleId;
    ModuleId* NativeClrModuleId;
};

const std::string SymbolsStore::NotResolvedModuleName = "NotResolvedModule";
const std::string SymbolsStore::NotResolvedFrame = "NotResolvedFrame";
const std::string SymbolsStore::UnloadedModuleName = "UnloadedModule";
const std::string SymbolsStore::FakeModuleName = "FakeModule";

const std::string SymbolsStore::FakeContentionFrame = "|lm:Unknown-Assembly |ns: |ct:Unknown-Type |cg: |fn:lock-contention |fg: |sg:(?)";
const std::string SymbolsStore::FakeAllocationFrame = "|lm:Unknown-Assembly |ns: |ct:Unknown-Type |cg: |fn:allocation |fg: |sg:(?)";

const std::string SymbolsStore::UnknownNativeFrame = "|lm:Unknown-Native-Module |ns:NativeCode |ct:Unknown-Native-Module |fn:Function";
const std::string SymbolsStore::UnknownNativeModule = "Unknown-Native-Module";

const std::string SymbolsStore::UnknownManagedFrame = "|lm:Unknown-Assembly |ns: |ct:Unknown-Type |cg: |fn:Unknown-Method |fg: |sg:(?)";
const std::string SymbolsStore::UnknownManagedType = "|lm:Unknown-Assembly |ns: |ct:Unknown-Type |cg: ";
const std::string SymbolsStore::UnknownManagedAssembly = "Unknown-Assembly";

// define well known label string constants
const std::string SymbolsStore::ThreadIdLabel = "thread id";
const std::string SymbolsStore::ThreadNameLabel = "thread name";
const std::string SymbolsStore::AppDomainNameLabel = "appdomain name";
const std::string SymbolsStore::ProcessIdLabel = "appdomain process id";
const std::string SymbolsStore::LocalRootSpanIdLabel = "local root span id";
const std::string SymbolsStore::SpanIdLabel = "span id";
const std::string SymbolsStore::ExceptionTypeLabel = "exception type";
const std::string SymbolsStore::ExceptionMessageLabel = "exception message";
const std::string SymbolsStore::AllocationClassLabel = "allocation class";

// garbage collection related labels
const std::string SymbolsStore::TimelineEventTypeLabel = "event";
const std::string SymbolsStore::TimelineEventTypeThreadStart = "thread start";
const std::string SymbolsStore::TimelineEventTypeThreadStop = "thread stop";
const std::string SymbolsStore::TimelineEventTypeStopTheWorld = "stw";
const std::string SymbolsStore::TimelineEventTypeGarbageCollection = "gc";
const std::string SymbolsStore::GarbageCollectionReasonLabel = "gc reason";         // look at GCReason enumeration
const std::string SymbolsStore::GarbageCollectionTypeLabel = "gc type";             // look at GCType enumeration
const std::string SymbolsStore::GarbageCollectionCompactingLabel = "gc compacting"; // true or false
const std::string SymbolsStore::GarbageCollectionGenerationLabel = "gc generation";
const std::string SymbolsStore::GarbageCollectionNumberLabel = "gc number";

// life object related labels
const std::string SymbolsStore::ObjectLifetimeLabel = "object lifetime";
const std::string SymbolsStore::ObjectIdLabel = "object id";
const std::string SymbolsStore::ObjectGenerationLabel = "object generation";

// network requests related labels
const std::string SymbolsStore::RequestUrlLabel = "request url";
const std::string SymbolsStore::RequestStatusCodeLabel = "response status code";
const std::string SymbolsStore::RequestErrorLabel = "response error";
const std::string SymbolsStore::RequestRedirectUrlLabel = "redirect url";
const std::string SymbolsStore::RequestDnsWaitLabel = "dns.wait";
const std::string SymbolsStore::RequestDnsDurationLabel = "dns.duration";
const std::string SymbolsStore::RequestDnsSuccessLabel = "dns.success";
const std::string SymbolsStore::RequestHandshakeWaitLabel = "tls.wait";
const std::string SymbolsStore::RequestHandshakeDurationLabel = "tls.duration";
const std::string SymbolsStore::RequestHandshakeErrorLabel = "tls.error";
const std::string SymbolsStore::RequestSocketDurationLabel = "socket.duration";
const std::string SymbolsStore::RequestDurationLabel = "request.duration";
const std::string SymbolsStore::ResponseContentDurationLabel = "response_content.duration";
const std::string SymbolsStore::RequestResponseThreadIdLabel = "response.thread_id";
const std::string SymbolsStore::RequestResponseThreadNameLabel = "response.thread_name";

const std::string SymbolsStore::GcCpuThreadLabel = "gc_cpu_sample";

const std::string SymbolsStore::BucketLabelName = "Duration bucket";
const std::string SymbolsStore::WaitBucketLabelName = "Wait duration bucket";
const std::string SymbolsStore::RawCountLabelName = "raw count";
const std::string SymbolsStore::RawDurationLabelName = "raw duration";
const std::string SymbolsStore::BlockingThreadIdLabelName = "blocking thread id";
const std::string SymbolsStore::BlockingThreadNameLabelName = "blocking thread name";
const std::string SymbolsStore::ContentionTypeLabelName = "contention type";

const std::string SymbolsStore::ThreadStartFrame = "|lm: |ns: |ct: |cg: |fn:Thread Start |fg: |sg:";
const std::string SymbolsStore::ThreadStopFrame = "|lm: |ns: |ct: |cg: |fn:Thread Stop |fg: |sg:";

struct SymbolsStore::SymbolsStoreImpl
{
    ddog_prof_ProfilesDictionaryHandle symbols;
    KnownSymbols knownSymbols;
};

SymbolsStore::SymbolsStore() :
    _impl(nullptr)
{
}

SymbolsStore::~SymbolsStore() = default;

const char* SymbolsStore::GetName()
{
    return "SymbolsStore";
}

bool SymbolsStore::StartImpl()
{
    auto ptr = std::make_unique<SymbolsStoreImpl>();
    _impl = ptr.get();

    auto status = ddog_prof_ProfilesDictionary_new(&ptr->symbols);
    if (status.err != nullptr)
    {
        auto error = make_error(status);
        Log::Error("Failed to create symbol store: ", error.message());
        return false;
    }

    // pre-intern known symbols
    if (!RegisterKnownStuffs())
    {
        ddog_prof_ProfilesDictionary_drop(&ptr->symbols);
        return false;
    }

    // we transfer ownership only if the start is successful
    _impl = ptr.release();
    return true;
}

#define INTERN_STRING(str, fieldName)                   \
    {                                                   \
        auto result = InternString(str);                \
        if (!result)                                    \
        {                                               \
            return false;                               \
        }                                               \
        _impl->knownSymbols.fieldName = result.value(); \
    }

bool SymbolsStore::RegisterKnownStuffs()
{
    INTERN_STRING(ThreadIdLabel, ThreadId);
    INTERN_STRING(ThreadNameLabel, ThreadName);
    INTERN_STRING(ProcessIdLabel, ProcessId);
    INTERN_STRING(AppDomainNameLabel, AppDomainName);
    INTERN_STRING(LocalRootSpanIdLabel, LocalRootSpanId);
    INTERN_STRING(SpanIdLabel, SpanId);
    INTERN_STRING(ExceptionTypeLabel, ExceptionType);
    INTERN_STRING(ExceptionMessageLabel, ExceptionMessage);
    INTERN_STRING(AllocationClassLabel, AllocationClass);
    INTERN_STRING(GarbageCollectionGenerationLabel, GarbageCollectionGeneration);
    INTERN_STRING(GarbageCollectionNumberLabel, GarbageCollectionNumber);
    INTERN_STRING(TimelineEventTypeLabel, TimelineEventType);
    INTERN_STRING(TimelineEventTypeStopTheWorld, TimelineEventTypeStopTheWorld);
    INTERN_STRING(TimelineEventTypeGarbageCollection, TimelineEventTypeGarbageCollection);
    INTERN_STRING(TimelineEventTypeThreadStart, TimelineEventTypeThreadStart);
    INTERN_STRING(TimelineEventTypeThreadStop, TimelineEventTypeThreadStop);
    INTERN_STRING(GarbageCollectionReasonLabel, GarbageCollectionReason);
    INTERN_STRING(GarbageCollectionTypeLabel, GarbageCollectionType);
    INTERN_STRING(GarbageCollectionCompactingLabel, GarbageCollectionCompacting);
    INTERN_STRING(ObjectLifetimeLabel, ObjectLifetime);
    INTERN_STRING(ObjectIdLabel, ObjectId);
    INTERN_STRING(ObjectGenerationLabel, ObjectGeneration);
    INTERN_STRING(RequestUrlLabel, RequestUrl);
    INTERN_STRING(RequestStatusCodeLabel, RequestStatusCode);
    INTERN_STRING(RequestErrorLabel, RequestError);
    INTERN_STRING(RequestRedirectUrlLabel, RequestRedirectUrl);
    INTERN_STRING(RequestDnsWaitLabel, RequestDnsWait);
    INTERN_STRING(RequestDnsDurationLabel, RequestDnsDuration);
    INTERN_STRING(RequestDnsSuccessLabel, RequestDnsSuccess);
    INTERN_STRING(RequestHandshakeWaitLabel, RequestHandshakeWait);
    INTERN_STRING(RequestHandshakeDurationLabel, RequestHandshakeDuration);
    INTERN_STRING(RequestHandshakeErrorLabel, RequestHandshakeError);
    INTERN_STRING(RequestSocketDurationLabel, RequestSocketDuration);
    INTERN_STRING(RequestDurationLabel, RequestDuration);
    INTERN_STRING(ResponseContentDurationLabel, ResponseContentDuration);
    INTERN_STRING(RequestResponseThreadIdLabel, RequestResponseThreadId);
    INTERN_STRING(RequestResponseThreadNameLabel, RequestResponseThreadName);

    INTERN_STRING(GcCpuThreadLabel, GcCpuThread);

    INTERN_STRING(BucketLabelName, BucketLabelName);
    INTERN_STRING(WaitBucketLabelName, WaitBucketLabelName);
    INTERN_STRING(RawCountLabelName, RawCountLabelName);
    INTERN_STRING(RawDurationLabelName, RawDurationLabelName);
    INTERN_STRING(BlockingThreadIdLabelName, BlockingThreadIdLabelName);
    INTERN_STRING(BlockingThreadNameLabelName, BlockingThreadNameLabelName);
    INTERN_STRING(ContentionTypeLabelName, ContentionTypeLabelName);

    auto unresolvedFrameId = InternFunction(NotResolvedFrame, ""); // filename is empty
    if (!unresolvedFrameId)
    {
        return false;
    }
    _impl->knownSymbols.NotResolvedFrameId = unresolvedFrameId.value();

    auto threadStartFrameId = InternFunction(ThreadStartFrame, "");
    if (!threadStartFrameId)
    {
        return false;
    }
    _impl->knownSymbols.ThreadStartFrame = threadStartFrameId.value();

    auto threadStopFrameId = InternFunction(ThreadStopFrame, "");
    if (!threadStopFrameId)
    {
        return false;
    }
    _impl->knownSymbols.ThreadStopFrame = threadStopFrameId.value();

    auto unresolvedModuleId = InternMapping(NotResolvedModuleName);
    if (!unresolvedModuleId)
    {
        return false;
    }
    _impl->knownSymbols.NotResolvedModuleId = unresolvedModuleId.value();

    auto unloadedModuleId = InternMapping(UnloadedModuleName);
    if (!unloadedModuleId)
    {
        return false;
    }
    _impl->knownSymbols.UnloadedModuleId = unloadedModuleId.value();

    auto fakeModuleId = InternMapping(FakeModuleName);
    if (!fakeModuleId)
    {
        return false;
    }
    _impl->knownSymbols.FakeModuleId = fakeModuleId.value();

    auto fakeFunctionId = InternFunction(FakeContentionFrame, ""); // filename is empty
    if (!fakeFunctionId)
    {
        return false;
    }
    _impl->knownSymbols.FakeFunctionId = fakeFunctionId.value();

    auto fakeContentionFrameId = InternFunction(FakeContentionFrame, "");
    if (!fakeContentionFrameId)
    {
        return false;
    }
    _impl->knownSymbols.FakeContentionFrameId = fakeContentionFrameId.value();

    auto unknownNativeFrameId = InternFunction(UnknownNativeFrame, "");
    if (!unknownNativeFrameId)
    {
        return false;
    }
    _impl->knownSymbols.UnknownNativeFrameId = unknownNativeFrameId.value();

    auto unknownNativeModuleId = InternMapping(UnknownNativeModule);
    if (!unknownNativeModuleId)
    {
        return false;
    }
    _impl->knownSymbols.UnknownNativeModuleId = unknownNativeModuleId.value();

    auto unknownManagedFrameId = InternFunction(UnknownManagedFrame, "");
    if (!unknownManagedFrameId)
    {
        return false;
    }
    _impl->knownSymbols.UnknownManagedFrameId = unknownManagedFrameId.value();

    auto unknownManagedTypeId = InternString(UnknownManagedType);
    if (!unknownManagedTypeId)
    {
        return false;
    }
    _impl->knownSymbols.UnknownManagedTypeId = unknownManagedTypeId.value();

    auto unknownManagedAssemblyId = InternMapping(UnknownManagedAssembly);
    if (!unknownManagedAssemblyId)
    {
        return false;
    }
    _impl->knownSymbols.UnknownManagedAssemblyId = unknownManagedAssemblyId.value();

    auto fakeAllocationFrameId = InternFunction(FakeAllocationFrame, "");
    if (!fakeAllocationFrameId)
    {
        return false;
    }
    _impl->knownSymbols.FakeAllocationFrameId = fakeAllocationFrameId.value();

    auto clrModuleId = InternMapping("CLR");
    if (!clrModuleId)
    {
        return false;
    }
    _impl->knownSymbols.ClrModuleId = clrModuleId.value();

    auto gen0FrameId = InternFunction("|lm: |ns: |ct: |cg: |fn:gen0 |fg: |sg:", "");
    if (!gen0FrameId)
    {
        return false;
    }
    _impl->knownSymbols.Gen0FrameId = gen0FrameId.value();

    auto gen1FrameId = InternFunction("|lm: |ns: |ct: |cg: |fn:gen1 |fg: |sg:", "");
    if (!gen1FrameId)
    {
        return false;
    }
    _impl->knownSymbols.Gen1FrameId = gen1FrameId.value();

    auto gen2FrameId = InternFunction("|lm: |ns: |ct: |cg: |fn:gen2 |fg: |sg:", "");
    if (!gen2FrameId)
    {
        return false;
    }
    _impl->knownSymbols.Gen2FrameId = gen2FrameId.value();

    auto unknownGenFrameId = InternFunction("|lm: |ns: |ct: |cg: |fn:unknown |fg: |sg:", "");
    if (!unknownGenFrameId)
    {
        return false;
    }
    _impl->knownSymbols.UnknownGenFrameId = unknownGenFrameId.value();

    auto gcRootFrameId = InternFunction("|lm: |ns: |ct: |cg: |fn:Garbage Collector |fg: |sg:", "");
    if (!gcRootFrameId)
    {
        return false;
    }
    _impl->knownSymbols.GCRootFrameId = gcRootFrameId.value();

    auto nativeGcRootFrameId = InternFunction("|lm:[native] |ns: |ct: |cg: |fn:Garbage Collector |fg: |sg:", "");
    if (!nativeGcRootFrameId)
    {
        return false;
    }
    _impl->knownSymbols.NativeGCRootFrameId = nativeGcRootFrameId.value();

    auto dotNetRootFrameId = InternFunction("|lm: |ns: |ct: |cg: |fn:.NET |fg: |sg:", "");
    if (!dotNetRootFrameId)
    {
        return false;
    }
    _impl->knownSymbols.DotNetRootFrameId = dotNetRootFrameId.value();

    auto nativeDotNetRootFrameId = InternFunction("|lm:[native] |ns: |ct: |cg: |fn:.NET |fg: |sg:", "");
    if (!nativeDotNetRootFrameId)
    {
        return false;
    }
    _impl->knownSymbols.NativeDotNetRootFrameId = nativeDotNetRootFrameId.value();

    auto nativeClrModuleId = InternMapping("[native] CLR");
    if (!nativeClrModuleId)
    {
        return false;
    }
    _impl->knownSymbols.NativeClrModuleId = nativeClrModuleId.value();

    auto nativeGcModuleId = InternMapping("[native] GC");
    if (!nativeGcModuleId)
    {
        return false;
    }
    _impl->knownSymbols.NativeGcModuleId = nativeGcModuleId.value();

    return true;
}

bool SymbolsStore::StopImpl()
{
    // use unique_ptr for exception safety
    auto old = std::unique_ptr<SymbolsStoreImpl>(std::exchange(_impl, nullptr));
    if (old == nullptr)
    {
        // TO review: we should never end this. Because the profiler should start and use the SymbolsStore if
        // the symbols store failed to start up.
        return false;
    }

    ddog_prof_ProfilesDictionary_drop(&old->symbols);
    return true;
}

// returns a StringId
// std::optional instead
std::optional<StringId*> SymbolsStore::InternString(std::string_view str)
{
    // TODO error if not started
    ddog_prof_StringId2 id;
    auto status = ddog_prof_ProfilesDictionary_insert_str(&id, _impl->symbols, libdatadog::to_char_slice(str), DDOG_PROF_UTF8_OPTION_ASSUME);
    if (status.err != nullptr)
    {
        auto error = make_error(status);
        Log::Error("Failed to intern string: ", error.message());
        return std::nullopt;
    }
    return {reinterpret_cast<StringId*>(id)};
}

// returns std::optional
std::optional<FunctionId*> SymbolsStore::InternFunction(std::string const& functionName, std::string_view fileName)
{
    ddog_prof_FunctionId2 id;
    auto nameId = InternString(functionName);
    if (!nameId)
    {
        return std::nullopt;
    }
    auto filenameId = InternString(fileName);
    if (!filenameId)
    {
        return std::nullopt;
    }
    auto fn = ddog_prof_Function2{
        .name = reinterpret_cast<ddog_prof_StringId2>(nameId.value()) ,
        .system_name = DDOG_PROF_STRINGID2_EMPTY,
        .file_name = reinterpret_cast<ddog_prof_StringId2>(filenameId.value())
    };

    auto status = ddog_prof_ProfilesDictionary_insert_function(&id, _impl->symbols, &fn);
    if (status.err != nullptr)
    {
        auto error = make_error(status);
        Log::Error("Failed to intern mapping: ", error.message());
        return std::nullopt;
    }
    return {reinterpret_cast<FunctionId*>(id)};
}

std::optional<ModuleId*> SymbolsStore::InternMapping(std::string const& moduleName)
{
    ddog_prof_MappingId2 id{};
    auto nameId = InternString(moduleName);
    if (!nameId)
    {
        return std::nullopt;
    }
    auto mapping = ddog_prof_Mapping2{
        .memory_start = 0,
        .memory_limit = 0,
        .file_offset = 0,
        .filename = reinterpret_cast<ddog_prof_StringId2>(nameId.value()),
        .build_id = DDOG_PROF_STRINGID2_EMPTY};

    // TODO check errors
    auto status = ddog_prof_ProfilesDictionary_insert_mapping(&id, _impl->symbols, &mapping);
    if (status.err != nullptr)
    {
        auto error = make_error(status);
        Log::Error("Failed to intern mapping: ", error.message());
        return std::nullopt;
    }
    return {reinterpret_cast<ModuleId*>(id)};
}

FunctionId* SymbolsStore::GetNotResolvedFrameId()
{
    return _impl->knownSymbols.NotResolvedFrameId;
}

ModuleId* SymbolsStore::GetNotResolvedModuleId()
{
    return _impl->knownSymbols.NotResolvedModuleId;
}

ModuleId* SymbolsStore::GetUnloadedModuleId()
{
    return _impl->knownSymbols.UnloadedModuleId;
}

ModuleId* SymbolsStore::GetFakeModuleId()
{
    return _impl->knownSymbols.FakeModuleId;
}

FunctionId* SymbolsStore::GetFakeFunctionId()
{
    return _impl->knownSymbols.FakeFunctionId;
}

FunctionId* SymbolsStore::GetFakeContentionFrameId()
{
    return _impl->knownSymbols.FakeContentionFrameId;
}

FunctionId* SymbolsStore::GetFakeAllocationFrameId()
{
    return _impl->knownSymbols.FakeAllocationFrameId;
}

ModuleId* SymbolsStore::GetUnknownNativeModuleId()
{
    return _impl->knownSymbols.UnknownNativeModuleId;
}

FunctionId* SymbolsStore::GetUnknownNativeFrameId()
{
    return _impl->knownSymbols.UnknownNativeFrameId;
}

// should be function id instead of frame id
FunctionId* SymbolsStore::GetUnknownManagedFrameId()
{
    return _impl->knownSymbols.UnknownManagedFrameId;
}

StringId* SymbolsStore::GetUnknownManagedTypeId()
{
    return _impl->knownSymbols.UnknownManagedTypeId;
}

ModuleId* SymbolsStore::GetUnknownManagedAssemblyId()
{
    return _impl->knownSymbols.UnknownManagedAssemblyId;
}

ModuleId* SymbolsStore::GetClrModuleId()
{
    return _impl->knownSymbols.ClrModuleId;
}

FunctionId* SymbolsStore::GetGen0FrameId()
{
    return _impl->knownSymbols.Gen0FrameId;
}

FunctionId* SymbolsStore::GetGen1FrameId()
{
    return _impl->knownSymbols.Gen1FrameId;
}

FunctionId* SymbolsStore::GetGen2FrameId()
{
    return _impl->knownSymbols.Gen2FrameId;
}

FunctionId* SymbolsStore::GetUnknownGenFrameId()
{
    return _impl->knownSymbols.UnknownGenFrameId;
}

FunctionId* SymbolsStore::GetGCRootFrameId()
{
    return _impl->knownSymbols.GCRootFrameId;
}

FunctionId* SymbolsStore::GetNativeGCRootFrameId()
{
    return _impl->knownSymbols.NativeGCRootFrameId;
}

FunctionId* SymbolsStore::GetDotNetRootFrameId()
{
    return _impl->knownSymbols.DotNetRootFrameId;
}

FunctionId* SymbolsStore::GetNativeDotNetRootFrameId()
{
    return _impl->knownSymbols.NativeDotNetRootFrameId;
}

FunctionId* SymbolsStore::GetThreadStartFrame()
{
    return _impl->knownSymbols.ThreadStartFrame;
}

FunctionId* SymbolsStore::GetThreadStopFrame()
{
    return _impl->knownSymbols.ThreadStopFrame;
}

ProfileDictionary* SymbolsStore::GetDictionary()
{
    return reinterpret_cast<ProfileDictionary*>(_impl->symbols);
}

ModuleId* SymbolsStore::GetNativeGcModuleId()
{
    return _impl->knownSymbols.NativeGcModuleId;
}

ModuleId* SymbolsStore::GetNativeClrModuleId()
{
    return _impl->knownSymbols.NativeClrModuleId;
}

#define GET_KNOWN_SYMBOL(fieldName)                          \
    StringId* SymbolsStore::Get##fieldName() const \
    {                                                        \
        return _impl->knownSymbols.fieldName;                \
    }

GET_KNOWN_SYMBOL(ThreadId);
GET_KNOWN_SYMBOL(ThreadName);
GET_KNOWN_SYMBOL(ProcessId);
GET_KNOWN_SYMBOL(AppDomainName);
GET_KNOWN_SYMBOL(LocalRootSpanId);
GET_KNOWN_SYMBOL(SpanId);
GET_KNOWN_SYMBOL(ExceptionType);
GET_KNOWN_SYMBOL(ExceptionMessage);
GET_KNOWN_SYMBOL(AllocationClass);
GET_KNOWN_SYMBOL(GarbageCollectionGeneration);
GET_KNOWN_SYMBOL(GarbageCollectionNumber);
GET_KNOWN_SYMBOL(TimelineEventType);
GET_KNOWN_SYMBOL(TimelineEventTypeStopTheWorld);
GET_KNOWN_SYMBOL(TimelineEventTypeGarbageCollection);
GET_KNOWN_SYMBOL(TimelineEventTypeThreadStart);
GET_KNOWN_SYMBOL(TimelineEventTypeThreadStop);
GET_KNOWN_SYMBOL(GarbageCollectionReason);
GET_KNOWN_SYMBOL(GarbageCollectionType);
GET_KNOWN_SYMBOL(GarbageCollectionCompacting);
GET_KNOWN_SYMBOL(ObjectLifetime);
GET_KNOWN_SYMBOL(ObjectId);
GET_KNOWN_SYMBOL(ObjectGeneration);
GET_KNOWN_SYMBOL(RequestUrl);
GET_KNOWN_SYMBOL(RequestStatusCode);
GET_KNOWN_SYMBOL(RequestError);
GET_KNOWN_SYMBOL(RequestRedirectUrl);
GET_KNOWN_SYMBOL(RequestDnsWait);
GET_KNOWN_SYMBOL(RequestDnsDuration);
GET_KNOWN_SYMBOL(RequestDnsSuccess);
GET_KNOWN_SYMBOL(RequestHandshakeWait);
GET_KNOWN_SYMBOL(RequestHandshakeDuration);
GET_KNOWN_SYMBOL(RequestHandshakeError);
GET_KNOWN_SYMBOL(RequestSocketDuration);
GET_KNOWN_SYMBOL(RequestDuration);
GET_KNOWN_SYMBOL(ResponseContentDuration);
GET_KNOWN_SYMBOL(RequestResponseThreadId);
GET_KNOWN_SYMBOL(RequestResponseThreadName);
GET_KNOWN_SYMBOL(GcCpuThread);

GET_KNOWN_SYMBOL(BucketLabelName);
GET_KNOWN_SYMBOL(WaitBucketLabelName);
GET_KNOWN_SYMBOL(RawCountLabelName);
GET_KNOWN_SYMBOL(RawDurationLabelName);
GET_KNOWN_SYMBOL(BlockingThreadIdLabelName);
GET_KNOWN_SYMBOL(BlockingThreadNameLabelName);
GET_KNOWN_SYMBOL(ContentionTypeLabelName);
} // namespace libdatadog