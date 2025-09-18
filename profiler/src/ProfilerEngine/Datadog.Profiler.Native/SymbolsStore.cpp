#include "SymbolsStore.h"
#include "FfiHelper.h"

#include <vector>
#include <variant>

extern "C" {
    #include "datadog/common.h"
    #include "datadog/profiling.h"
}

namespace libdatadog
{

    struct KnownSymbols
    {
        ddog_prof_FunctionId NotResolvedFrameId;
        ddog_prof_MappingId NotResolvedModuleId;
        ddog_prof_MappingId UnloadedModuleId;
        ddog_prof_MappingId UnknownNativeModuleId;

        ddog_prof_MappingId FakeModuleId;

        ddog_prof_FunctionId FakeContentionFrameId;
        ddog_prof_FunctionId FakeAllocationFrameId;

        ddog_prof_FunctionId FakeFunctionId;
        ddog_prof_FunctionId UnknownNativeFrameId;
        ddog_prof_FunctionId UnknownManagedFrameId;
        ddog_prof_StringId UnknownManagedTypeId;
        ddog_prof_MappingId UnknownManagedAssemblyId;
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

    struct SymbolsStore::SymbolsStoreImpl
    {
        ddog_prof_ProfilesDictionaryHandle symbols;
        KnownSymbols knownSymbols;
    };

    SymbolsStore::SymbolsStore()
        : _impl(nullptr)
    {
    }

    SymbolsStore::~SymbolsStore() = default;

    const char* SymbolsStore::GetName()
    {
        return "SymbolsStore";
    }

    bool SymbolsStore::StartImpl()
    {
        _impl = std::make_unique<SymbolsStoreImpl>();
        // handle errors
        ddog_prof_ProfilesDictionary_new(&_impl->symbols);

        // pre-intern known symbols
        if (!RegisterKnownStuffs()) return false;

        return true;
    }

    bool SymbolsStore::RegisterKnownStuffs()
    {
        auto unresolvedFrameId = InternFunction(NotResolvedFrame, ""); // filename is empty
        if (!unresolvedFrameId)
        {
            return false;
        }
        _impl->knownSymbols.NotResolvedFrameId = unresolvedFrameId.value();

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
        if (!unknownNativeFrameId == 0)
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
        _impl->knownSymbols.UnknownNativeFrameId = unknownManagedFrameId.value();

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
        return true;
    }

    bool SymbolsStore::StopImpl()
    {
        return true;
    }

    // returns a stringId
    // std::optional instead
    std::optional<ddog_prof_StringId> SymbolsStore::InternString(std::string_view str)
    {
        // TODO error if not started
        ddog_prof_StringId id{};
        // TODO: handle errors
        ddog_prof_ProfilesDictionary_insert_str(&id, _impl->symbols, libdatadog::to_char_slice(str), DDOG_PROF_UTF8_OPTION_VALIDATE);
        return {id};
    }

    // returns std::optional 
    std::optional<ddog_prof_FunctionId> SymbolsStore::InternFunction(std::string const& functionName, std::string_view fileName)
    {
        ddog_prof_FunctionId id{};
        auto name = InternString(functionName);
        auto filename = InternString(fileName);
        auto fn = ddog_prof_Function{
            .name = name.value(),
            .system_name = DDOG_PROF_STRINGID_EMPTY,
            .file_name = filename.value()};

        // TODO check errors
        ddog_prof_ProfilesDictionary_insert_function(&id, _impl->symbols, &fn);
        return {id};
    }

    std::optional<ddog_prof_MappingId> SymbolsStore::InternMapping(std::string const& moduleName)
    {
        ddog_prof_MappingId id{};
        auto name = InternString(moduleName);
        // todo check errors
        auto mapping = ddog_prof_Mapping{
            .memory_start = 0,
            .memory_limit = 0,
            .file_offset = 0,
            .filename = name.value(),
            .build_id = DDOG_PROF_STRINGID_EMPTY};

        // TODO check errors
        ddog_prof_ProfilesDictionary_insert_mapping(&id, _impl->symbols, &mapping);
        return {id};
    }

    ddog_prof_FunctionId SymbolsStore::GetNotResolvedFrameId()
    {
        return _impl->knownSymbols.NotResolvedFrameId;
    }

    ddog_prof_MappingId SymbolsStore::GetNotResolvedModuleId()
    {
        return _impl->knownSymbols.NotResolvedModuleId;
    }

    ddog_prof_MappingId SymbolsStore::GetUnloadedModuleId()
    {
        return _impl->knownSymbols.UnloadedModuleId;
    }

    ddog_prof_MappingId SymbolsStore::GetFakeModuleId()
    {
        return _impl->knownSymbols.FakeModuleId;
    }

    ddog_prof_FunctionId SymbolsStore::GetFakeFunctionId()
    {
        return _impl->knownSymbols.FakeFunctionId;
    }

    ddog_prof_FunctionId SymbolsStore::GetFakeContentionFrameId()
    {
        return _impl->knownSymbols.FakeContentionFrameId;
    }

    ddog_prof_FunctionId SymbolsStore::GetFakeAllocationFrameId()
    {
        return _impl->knownSymbols.FakeAllocationFrameId;
    }

    ddog_prof_MappingId SymbolsStore::GetUnknownNativeModuleId()
    {
        return _impl->knownSymbols.UnknownNativeModuleId;
    }

    ddog_prof_FunctionId SymbolsStore::GetUnknownNativeFrameId()
    {
        return _impl->knownSymbols.UnknownNativeFrameId;
    }


    // should be function id instead of frame id
    ddog_prof_FunctionId SymbolsStore::GetUnknownManagedFrameId()
    {
        return _impl->knownSymbols.UnknownManagedFrameId;
    }

    ddog_prof_StringId SymbolsStore::GetUnknownManagedTypeId()
    {
        return _impl->knownSymbols.UnknownManagedTypeId;
    }

    ddog_prof_MappingId SymbolsStore::GetUnknownManagedAssemblyId()
    {
        return _impl->knownSymbols.UnknownManagedAssemblyId;
    }
} // namespace libdatadog
