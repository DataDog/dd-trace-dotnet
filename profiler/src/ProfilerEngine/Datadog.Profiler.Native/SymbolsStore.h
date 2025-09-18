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

        std::optional<ddog_prof_StringId> InternString(std::string_view str);
        std::optional<ddog_prof_FunctionId> InternFunction(std::string const& functionName, std::string_view fileName);
        std::optional<ddog_prof_MappingId> InternMapping(std::string const& moduleName);

        ddog_prof_FunctionId GetNotResolvedFrameId();
        ddog_prof_MappingId GetNotResolvedModuleId();
        ddog_prof_MappingId GetUnloadedModuleId();
        ddog_prof_FunctionId GetFakeContentionFrameId();
        ddog_prof_FunctionId GetFakeAllocationFrameId();

        ddog_prof_FunctionId GetFakeFunctionId();
        ddog_prof_MappingId GetFakeModuleId();
        ddog_prof_FunctionId GetUnknownNativeFrameId();
        ddog_prof_MappingId GetUnknownNativeModuleId();
        ddog_prof_FunctionId GetUnknownManagedFrameId();
        ddog_prof_StringId GetUnknownManagedTypeId();
        ddog_prof_MappingId GetUnknownManagedAssemblyId();

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


        bool RegisterKnownStuffs();

        struct SymbolsStoreImpl;
        std::unique_ptr<SymbolsStoreImpl> _impl;
    };
}
