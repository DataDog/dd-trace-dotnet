// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "Profile.h"

#include "SymbolsStore.h"
#include "FfiHelper.h"
#include "IConfiguration.h"
#include "Log.h"
#include "ProfileImpl.hpp"
#include "Sample.h"

#include <chrono>

namespace libdatadog {

using namespace std::chrono_literals;

libdatadog::profile_unique_ptr CreateProfile(std::vector<SampleValueType> const& valueTypes, std::string const& periodType, std::string const& periodUnit, libdatadog::SymbolsStore* symbolsStore);

Profile::Profile(
    IConfiguration* configuration,
    std::vector<SampleValueType> const& valueTypes,
    std::string const& periodType,
    std::string const& periodUnit,
    std::string applicationName,
    libdatadog::SymbolsStore* symbolsStore)
    :
    _applicationName{std::move(applicationName)},
    _addTimestampOnSample{configuration->IsTimestampsAsLabelEnabled()},
    _symbolsStore{symbolsStore}
{
    _impl = CreateProfile(valueTypes, periodType, periodUnit, _symbolsStore);
}

Profile::~Profile() = default;

class SampleBuilder 
{
public:
    SampleBuilder(ddog_prof_ProfileAdapter* adapter, std::uint64_t grouping, ddog_Slice_I64 values_slice)
    {
        auto status = ddog_prof_ProfileAdapter_add_sample(&_inner, adapter, grouping, values_slice);
        if (status.flags != 0)
        {
            auto error = make_error(status);
            LogOnce(Warn, "Failed to create sample builder:", error.message(), " grouping id: ", grouping);
        }
        _isValid = status.flags == 0;
    }

    ~SampleBuilder()
    {
        if (_isValid)
        {
            ddog_prof_SampleBuilder_drop(&_inner);
        }
    }

    operator bool() const{
        return _isValid;
    }

    Success AddTimestamp(ddog_Timespec ts)
    {
        auto status = ddog_prof_SampleBuilder_timestamp(_inner, ts);
        if (status.flags != 0)
        {
            return make_error(status);
        }
        return make_success();
    }

    Success AddAttributeInt(ddog_prof_StringId nameId, std::int64_t value)
    {
        auto status = ddog_prof_SampleBuilder_attribute_int(_inner, nameId, value);
        if (status.flags != 0)
        {
            return make_error(status);
        }
        return make_success();
    }

    Success AddAttributeStr(ddog_prof_StringId nameId, ddog_CharSlice value)
    {
        auto status = ddog_prof_SampleBuilder_attribute_str(_inner, nameId, value, DDOG_PROF_UTF8_OPTION_ASSUME);
        if (status.flags != 0)
        {
            return make_error(status);
        }
        return make_success();
    }

    Success AddStackId(ddog_prof_StackId stackId)
    {
        auto status = ddog_prof_SampleBuilder_stack_id(_inner, stackId);
        if (status.flags != 0) {
            return make_error(status);
        }
        return make_success();
    }

    Success Finish()
    {
        _isValid = false;
        auto status = ddog_prof_SampleBuilder_finish(&_inner);
        if (status.flags != 0)
        {
            return make_error(status);
        }
        return make_success();
    }
private:
    ddog_prof_SampleBuilderHandle _inner;
    bool _isValid;
};

libdatadog::Success Profile::Add(std::shared_ptr<Sample> const& sample)
{
    auto const& callstack = sample->GetCallstack();
    auto nbFrames = callstack.size();

    auto& [locations, locationsSize, profilerAdapterHandle, scratchpad] = *_impl;

    auto groupingId = sample->GetGroupingId();
    // values
    auto const& values = sample->GetValues();
    ddog_Slice_I64 values_slice = {values.data(), values.size()};
    auto sampleBuilder = SampleBuilder(&profilerAdapterHandle, groupingId, values_slice);
    if (!sampleBuilder)
    {
        return make_error("No sample builder");
    }

    if (nbFrames > locationsSize)
    {
        locationsSize = nbFrames;
        locations.resize(nbFrames);
    }

    std::size_t idx = 0;
    for(auto const& frame : callstack)
    {
        ddog_prof_Line line = {.function_id = frame.FunctionId, .line_number = frame.StartLine};
        ddog_prof_Location loc = {.mapping_id = frame.ModuleId, .line = line};
        ddog_prof_LocationId locationId;
        auto status = ddog_prof_ScratchPad_insert_location(&locationId, scratchpad, &loc);
        if (status.flags != 0)
        {
            return make_error(status);
        }
        locations[idx] = locationId;
        idx++;
    }

    ddog_prof_Slice_LocationId loc_slice = {locations.data(), nbFrames};
    ddog_prof_StackId stackId;
    auto status = ddog_prof_ScratchPad_insert_stack(&stackId, scratchpad, loc_slice);
    if (status.flags != 0)
    {
        return make_error(status);
    }
    auto addStackSuccess = sampleBuilder.AddStackId(stackId);
    if (!addStackSuccess)
    {
        return addStackSuccess;
    }

    
    // add timestamp
    if (_addTimestampOnSample)
    {
        ddog_Timespec ts {};
        ts.seconds = sample->GetTimeStamp().count() / 1'000'000'000;
        ts.nanoseconds = sample->GetTimeStamp().count() % 1'000'000'000;
        auto success = sampleBuilder.AddTimestamp(ts);
        if (!success)
        {
            LogOnce(Error, "Unable to add timestamp to sample: ", success.message());
        }
    }

    auto labelsVisitor = LabelsVisitor{
        [&sampleBuilder, this](NumericLabel const& l) {
            auto const& [name, value] = l;
            // TODO back handle the case where there no nameId
            auto success = sampleBuilder.AddAttributeInt(
                name,
                value
            );
            if (!success)
            {
                LogOnce(Error, "Unable to add numeric label 'name' to sample: ", success.message());
            }
        },
        [&sampleBuilder, this](StringLabel const& l) {
            auto const& [name, value] = l;
            // TODO: check if we need to validate
            // TODO back handle the case where there no nameId
            auto success = sampleBuilder.AddAttributeStr(
                name,
                {value.data(), value.size()}
            );
            if (!success)
            {
                // TODO
                LogOnce(Error, "Unable to add string  label '' to sample: ", success.message());
            }
        }
    };

    
struct LabelsMyVisitor
{
    SampleBuilder* _sampleBuilder;

    void operator()(NumericLabel const& label)
    {
        auto const& [name, value] = label;
        // TODO back handle the case where there no nameId
        auto success = _sampleBuilder->AddAttributeInt(
            name,
            value
        );
        if (!success)
        {
            LogOnce(Error, "Unable to add numeric label 'name' to sample: ", success.message());
        }
    }
    
    void operator()(StringLabel const& label)
    {
        auto const& [name, value] = label;
        // TODO: check if we need to validate
        // TODO back handle the case where there no nameId
        auto success = _sampleBuilder->AddAttributeStr(
            name,
            {value.data(), value.size()}
        );
        if (!success)
        {
            // TODO
            LogOnce(Error, "Unable to add string  label '' to sample: ", success.message());
        }
    }
};

    LabelsMyVisitor labelsMyVisitor(&sampleBuilder);
    for (auto const& label : sample->GetLabels())
    {
        std::visit(labelsMyVisitor, label);
    }
    // todo: pass the symbols store
    //for (auto const& label : sample->GetLabels())
    //{
    //    std::visit(labelsVisitor, label);
    //}

    return sampleBuilder.Finish();
}

void Profile::SetEndpoint(int64_t traceId, std::string const& endpoint)
{
    auto endpointName = to_char_slice(endpoint);

    auto& [_, __, ___, scratchpad] = *_impl;
    ddog_prof_StringId endpointId;
    ddog_prof_ScratchPad_add_trace_endpoint_with_count(&endpointId, scratchpad, traceId, endpointName, DDOG_PROF_UTF8_OPTION_ASSUME, 1);
    //auto res = ddog_prof_Profile_set_endpoint(*_impl, traceId, endpointName);
    //if (res.tag == DDOG_PROF_PROFILE_RESULT_ERR)
    //{
    //    // this is needed even though we already logged: to free the allocated error message
    //    auto error = libdatadog::make_error(res.err);
    //    LogOnce(Info, "Unable to associate endpoint '", endpoint, "' to traced id '", traceId, "': ", error.message());
    //}
}


// definitly dead
void Profile::AddEndpointCount(std::string const& endpoint, int64_t count)
{
    //auto endpointName = to_char_slice(endpoint);

    //ddog_prof_ScratchPad_add_endpoint_count(_scratchpad, endpointName, count);
    //auto res = ddog_prof_Profile_add_endpoint_count(*_impl, endpointName, 1);
    //if (res.tag == DDOG_PROF_PROFILE_RESULT_ERR)
    //{
    //    // this is needed even though we already logged: to free the allocated error message
    //    auto error = libdatadog::make_error(res.err);
    //    LogOnce(Info, "Unable to add count for endpoint '", endpoint, "': ", error.message());
    //}
}

libdatadog::Success Profile::AddUpscalingRuleProportional(std::uint64_t groupingIndex, ddog_prof_StringId labelName, std::string_view groupName,
                                                          uint64_t sampled, uint64_t real)
{
    return make_success();
    ddog_CharSlice groupName_slice = to_char_slice(groupName);

    ddog_prof_GroupByLabel groupByLabel = {
        .key = labelName,
        .value = groupName_slice,
    };
    ddog_prof_ProportionalUpscalingRule upscalingRule = {
        .group_by_label = groupByLabel,
        .sampled = sampled,
        .real = real
    };

    struct ddog_prof_Slice_ProportionalUpscalingRule upscalingRule_slice = {&upscalingRule, 1};
 
    auto status = ddog_prof_ProfileAdapter_add_proportional_upscaling(*_impl, groupingIndex, upscalingRule_slice);
    if (status.flags != 0)
    {
        return make_error(status);
    }

//    auto upscalingRuleAdd = ddog_prof_Profile_add_upscaling_rule_proportional(*_impl, offsets_slice, labelName_slice, groupName_slice, sampled, real);
//    if (upscalingRuleAdd.tag == DDOG_PROF_PROFILE_RESULT_ERR)
//    {
//        // not great, we create 2 Success
//        // - the first one is to wrap the libdatadog error and ensure lifecycle is correctly handled
//        // - the second one is to provide the caller with the actual error.
//        // TODO: have a make_error(<format>, vars, ...) approach ?
//        auto error = make_error(upscalingRuleAdd.err);
//        std::stringstream ss;
//        ss << "(" << groupName << ", " << labelName << ") - [" << std::to_string(sampled) << "/" << std::to_string(real) << "]:"
//           << error.message();
//        return make_error(ss.str());
//    }

    return make_success();
}

libdatadog::Success Profile::AddUpscalingRulePoisson(std::uint64_t groupingIndex, std::string_view labelName, std::string_view groupName,
                                                          uintptr_t sumValueOffset, uintptr_t countValueOffset, uint64_t sampling_distance)
{
    ddog_prof_PoissonUpscalingRule upscalingRule = {.sum_offset = sumValueOffset, .count_offset = countValueOffset, .sampling_distance = sampling_distance};
    
    auto status = ddog_prof_ProfileAdapter_add_poisson_upscaling(*_impl, groupingIndex, upscalingRule);
    if (status.flags != 0)
    {
        return make_error(status);
    }

    return make_success();
}

libdatadog::profile_unique_ptr CreateProfile(std::vector<SampleValueType> const& valueTypes, std::string const& periodType, std::string const& periodUnit, SymbolsStore* symbolsStore)
{
    std::vector<ddog_prof_ValueType> samplesTypes;
    samplesTypes.reserve(valueTypes.size());

    // TODO: create a vector<int32> containing the indexes of the valueTypes
    std::vector<std::int64_t> groupings;
    groupings.reserve(valueTypes.size());

    for (auto const& type : valueTypes)
    {
        auto typeId = symbolsStore->InternString(type.Name);
        if (!typeId)
        {
            return nullptr;
        }
        auto unitId = symbolsStore->InternString(type.Unit);
        if (!unitId)
        {
            return nullptr;
        }

        samplesTypes.push_back(
            CreateValueType(
                typeId.value(),
                unitId.value()
            )
        );

        Log::Info("--- Grouping added: ", type.Index, " For ", type.Name);
        groupings.push_back(type.Index);
    }

    struct ddog_prof_Slice_ValueType sample_types = {samplesTypes.data(), samplesTypes.size()};
    struct ddog_Slice_I64 groupings_slice = {groupings.data(), groupings.size()};

    // wrapped into something that will hande its lifecycle
    ddog_prof_ScratchPadHandle scratchpad;
    auto status = ddog_prof_ScratchPad_new(&scratchpad);
    if (status.flags != 0)
    {
        auto error = make_error(status);
        LogOnce(Error, "Unable to create scratchpad: ", error.message());
        return nullptr;
    }

    // handle status
    ddog_prof_ProfileAdapter adapter;
    status = ddog_prof_ProfileAdapter_new(&adapter, symbolsStore->GetDictionary(), scratchpad, sample_types, groupings_slice);
    if (status.flags != 0)
    {
        auto error = make_error(status);
        LogOnce(Error, "Unable to create profile adapter: ", error.message());
        ddog_prof_ScratchPad_drop(&scratchpad);
        return nullptr;
    }

    return std::make_unique<ProfileImpl>(adapter, scratchpad);
}

std::string const& Profile::GetApplicationName() const
{
    return _applicationName;
}
} // namespace libdatadog
