#pragma once
#include <memory>
#include <optional>
#include <string>

#include "ServiceBase.h"

extern "C" {
    #include "datadog/common.h"
}

namespace libdatadog {

    /// 
    class SymbolsStore: public ServiceBase {
    public:
        SymbolsStore();
        ~SymbolsStore() override;
        const char* GetName() override;

        std::optional<ddog_prof_StringId2> InternString(std::string_view str);
        std::optional<ddog_prof_FunctionId2> InternFunction(std::string const& functionName, std::string_view fileName);
        std::optional<ddog_prof_MappingId2> InternMapping(std::string const& moduleName);

        ddog_prof_FunctionId2 GetNotResolvedFrameId();
        ddog_prof_MappingId2 GetNotResolvedModuleId();
        ddog_prof_MappingId2 GetUnloadedModuleId();
        ddog_prof_FunctionId2 GetFakeContentionFrameId();
        ddog_prof_FunctionId2 GetFakeAllocationFrameId();

        ddog_prof_FunctionId2 GetFakeFunctionId();
        ddog_prof_MappingId2 GetFakeModuleId();
        ddog_prof_FunctionId2 GetUnknownNativeFrameId();
        ddog_prof_MappingId2 GetUnknownNativeModuleId();
        ddog_prof_FunctionId2 GetUnknownManagedFrameId();
        ddog_prof_StringId2 GetUnknownManagedTypeId();
        ddog_prof_MappingId2 GetUnknownManagedAssemblyId();

        ddog_prof_MappingId2 GetClrModuleId();
        ddog_prof_FunctionId2 GetGen0FrameId();
        ddog_prof_FunctionId2 GetGen1FrameId();
        ddog_prof_FunctionId2 GetGen2FrameId();
        ddog_prof_FunctionId2 GetGCRootFrameId();
        ddog_prof_FunctionId2 GetDotNetRootFrameId();
        ddog_prof_ProfilesDictionaryHandle GetDictionary();
        ddog_prof_FunctionId2 GetThreadStartFrame();
        ddog_prof_FunctionId2 GetThreadStopFrame();

        ddog_prof_MappingId2 GetNativeGcModuleId();
        ddog_prof_MappingId2 GetNativeClrModuleId();

        #define DECLARE_GET_KNOWN_SYMBOL(fieldName) \
        ddog_prof_StringId2 Get##fieldName() const;

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
}