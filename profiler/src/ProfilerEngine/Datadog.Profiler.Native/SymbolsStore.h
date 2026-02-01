// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <memory>
#include <optional>
#include <string>

#include "ServiceBase.h"


namespace libdatadog {

// Used to hide libdatadog internal type.
struct StringId;
struct FunctionId;
struct ModuleId;
struct ProfileDictionary;

class SymbolsStore : public ServiceBase
{
public:
    SymbolsStore();
    ~SymbolsStore() override;
    const char* GetName() override;

    // TODO could we return a pointer instead an optional of a pointer ?
    // And have null as not found ?
    std::optional<StringId*> InternString(std::string_view str);
    std::optional<FunctionId*> InternFunction(std::string const& functionName, std::string_view fileName);
    std::optional<ModuleId*> InternMapping(std::string const& moduleName);

    FunctionId* GetNotResolvedFrameId();
    ModuleId* GetNotResolvedModuleId();
    ModuleId* GetUnloadedModuleId();
    FunctionId* GetFakeContentionFrameId();
    FunctionId* GetFakeAllocationFrameId();

    FunctionId* GetFakeFunctionId();
    ModuleId* GetFakeModuleId();
    FunctionId* GetUnknownNativeFrameId();
    ModuleId* GetUnknownNativeModuleId();
    FunctionId* GetUnknownManagedFrameId();
    StringId* GetUnknownManagedTypeId();
    ModuleId* GetUnknownManagedAssemblyId();

    ModuleId* GetClrModuleId();
    FunctionId* GetGen0FrameId();
    FunctionId* GetGen1FrameId();
    FunctionId* GetGen2FrameId();
    FunctionId* GetUnknownGenFrameId();
    FunctionId* GetGCRootFrameId();
    FunctionId* GetDotNetRootFrameId();
    ProfileDictionary* GetDictionary();
    FunctionId* GetThreadStartFrame();
    FunctionId* GetThreadStopFrame();

    ModuleId* GetNativeGcModuleId();
    ModuleId* GetNativeClrModuleId();
    FunctionId* GetNativeDotNetRootFrameId();
    FunctionId* GetNativeGCRootFrameId();

#define DECLARE_GET_KNOWN_SYMBOL(fieldName) \
    StringId* Get##fieldName() const;

    DECLARE_GET_KNOWN_SYMBOL(ThreadId);
    DECLARE_GET_KNOWN_SYMBOL(ThreadName);
    DECLARE_GET_KNOWN_SYMBOL(ProcessId);
    DECLARE_GET_KNOWN_SYMBOL(AppDomainName);
    DECLARE_GET_KNOWN_SYMBOL(LocalRootSpanId);
    DECLARE_GET_KNOWN_SYMBOL(SpanId);
    DECLARE_GET_KNOWN_SYMBOL(ExceptionType);
    DECLARE_GET_KNOWN_SYMBOL(ExceptionMessage);
    DECLARE_GET_KNOWN_SYMBOL(AllocationClass);
    DECLARE_GET_KNOWN_SYMBOL(GarbageCollectionGeneration);
    DECLARE_GET_KNOWN_SYMBOL(GarbageCollectionNumber);
    DECLARE_GET_KNOWN_SYMBOL(TimelineEventType);
    DECLARE_GET_KNOWN_SYMBOL(TimelineEventTypeStopTheWorld);
    DECLARE_GET_KNOWN_SYMBOL(TimelineEventTypeGarbageCollection);
    DECLARE_GET_KNOWN_SYMBOL(TimelineEventTypeThreadStart);
    DECLARE_GET_KNOWN_SYMBOL(TimelineEventTypeThreadStop);
    DECLARE_GET_KNOWN_SYMBOL(GarbageCollectionReason);
    DECLARE_GET_KNOWN_SYMBOL(GarbageCollectionType);
    DECLARE_GET_KNOWN_SYMBOL(GarbageCollectionCompacting);
    DECLARE_GET_KNOWN_SYMBOL(ObjectLifetime);
    DECLARE_GET_KNOWN_SYMBOL(ObjectId);
    DECLARE_GET_KNOWN_SYMBOL(ObjectGeneration);
    DECLARE_GET_KNOWN_SYMBOL(RequestUrl);
    DECLARE_GET_KNOWN_SYMBOL(RequestStatusCode);
    DECLARE_GET_KNOWN_SYMBOL(RequestError);
    DECLARE_GET_KNOWN_SYMBOL(RequestRedirectUrl);
    DECLARE_GET_KNOWN_SYMBOL(RequestDnsWait);
    DECLARE_GET_KNOWN_SYMBOL(RequestDnsDuration);
    DECLARE_GET_KNOWN_SYMBOL(RequestDnsSuccess);
    DECLARE_GET_KNOWN_SYMBOL(RequestHandshakeWait);
    DECLARE_GET_KNOWN_SYMBOL(RequestHandshakeDuration);
    DECLARE_GET_KNOWN_SYMBOL(RequestHandshakeError);
    DECLARE_GET_KNOWN_SYMBOL(RequestSocketDuration);
    DECLARE_GET_KNOWN_SYMBOL(RequestResponseThreadId);
    DECLARE_GET_KNOWN_SYMBOL(RequestResponseThreadName);
    DECLARE_GET_KNOWN_SYMBOL(RequestDuration);
    DECLARE_GET_KNOWN_SYMBOL(ResponseContentDuration);
    DECLARE_GET_KNOWN_SYMBOL(GcCpuThread);

    DECLARE_GET_KNOWN_SYMBOL(BucketLabelName);
    DECLARE_GET_KNOWN_SYMBOL(WaitBucketLabelName);
    DECLARE_GET_KNOWN_SYMBOL(RawCountLabelName);
    DECLARE_GET_KNOWN_SYMBOL(RawDurationLabelName);
    DECLARE_GET_KNOWN_SYMBOL(BlockingThreadIdLabelName);
    DECLARE_GET_KNOWN_SYMBOL(BlockingThreadNameLabelName);
    DECLARE_GET_KNOWN_SYMBOL(ContentionTypeLabelName);

protected:
    bool StartImpl() override;
    bool StopImpl() override;

private:
    // TODO: add those strings in the SymbolsStore
    static const std::string NotResolvedModuleName;
    static const std::string NotResolvedFrame;
    static const std::string UnloadedModuleName;
    static const std::string FakeModuleName;

    static const std::string FakeContentionFrame;
    static const std::string FakeAllocationFrame;
    static const std::string UnknownNativeFrame;
    static const std::string UnknownNativeModule;
    static const std::string UnknownManagedFrame;
    static const std::string UnknownManagedType;
    static const std::string UnknownManagedAssembly;

    static const std::string ThreadIdLabel;
    static const std::string ThreadNameLabel;
    static const std::string ProcessIdLabel;
    static const std::string AppDomainNameLabel;
    static const std::string LocalRootSpanIdLabel;
    static const std::string SpanIdLabel;
    static const std::string ExceptionTypeLabel;
    static const std::string ExceptionMessageLabel;
    static const std::string AllocationClassLabel;
    static const std::string GarbageCollectionGenerationLabel;
    static const std::string GarbageCollectionNumberLabel;
    static const std::string TimelineEventTypeLabel;
    static const std::string TimelineEventTypeStopTheWorld;
    static const std::string TimelineEventTypeGarbageCollection;
    static const std::string TimelineEventTypeThreadStart;
    static const std::string TimelineEventTypeThreadStop;
    static const std::string GarbageCollectionReasonLabel;
    static const std::string GarbageCollectionTypeLabel;
    static const std::string GarbageCollectionCompactingLabel;
    static const std::string ObjectLifetimeLabel;
    static const std::string ObjectIdLabel;
    static const std::string ObjectGenerationLabel;
    static const std::string RequestUrlLabel;
    static const std::string RequestStatusCodeLabel;
    static const std::string RequestErrorLabel;
    static const std::string RequestRedirectUrlLabel;
    static const std::string RequestDnsWaitLabel;
    static const std::string RequestDnsDurationLabel;
    static const std::string RequestDnsSuccessLabel;
    static const std::string RequestHandshakeWaitLabel;
    static const std::string RequestHandshakeDurationLabel;
    static const std::string RequestHandshakeErrorLabel;
    static const std::string RequestSocketDurationLabel;
    static const std::string RequestResponseThreadIdLabel;
    static const std::string RequestResponseThreadNameLabel;
    static const std::string RequestDurationLabel;
    static const std::string ResponseContentDurationLabel;

    static const std::string GcCpuThreadLabel;

    static const std::string BucketLabelName;
    static const std::string WaitBucketLabelName;
    static const std::string RawCountLabelName;
    static const std::string RawDurationLabelName;
    static const std::string BlockingThreadIdLabelName;
    static const std::string BlockingThreadNameLabelName;
    static const std::string ContentionTypeLabelName;
    static const std::string ThreadStartFrame;
    static const std::string ThreadStopFrame;

    bool RegisterKnownStuffs();

    struct SymbolsStoreImpl;
    SymbolsStoreImpl* _impl;
};
} // namespace libdatadog