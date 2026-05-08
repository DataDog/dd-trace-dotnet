// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "FfiHelper.h"
#include "Success.h"
#include "Tags.h"
#include "TagsImpl.hpp"

#include <cassert>
#include <memory>
#include <string>
#include <tuple>
#include <utility>
#include <vector>

extern "C"
{
#include "datadog/common.h"
#include "datadog/profiling.h"
}

namespace libdatadog {

class AgentProxy
{
public:
    AgentProxy(ddog_prof_ProfileExporter exporter) :
        _exporter{exporter}
    {
    }

    ~AgentProxy()
    {
        ddog_prof_Exporter_drop(&_exporter);
    }

    Success Send(ddog_prof_EncodedProfile* profile, Tags tags, std::vector<std::pair<std::string, std::string>> files, std::string metadata, std::string info, std::string processTags)
    {
        std::vector<ddog_prof_Exporter_File> to_compress_files;
        to_compress_files.reserve(files.size());

        for (auto& [filename, content] : files)
        {
            ddog_Slice_U8 fileSlice{reinterpret_cast<const uint8_t*>(content.c_str()), content.size()};
            to_compress_files.push_back({to_char_slice(filename), fileSlice});
        }

        ddog_prof_Exporter_Slice_File to_compress_files_view = {to_compress_files.data(), to_compress_files.size()};

        ddog_CharSlice* pMetadata = nullptr;
        ddog_CharSlice ffi_metadata{};
        if (!metadata.empty())
        {
            ffi_metadata = to_char_slice(metadata);
            pMetadata = &ffi_metadata;
        }

        ddog_CharSlice* pInfo = nullptr;
        ddog_CharSlice ffi_info{};
        if (!info.empty())
        {
            ffi_info = to_char_slice(info);
            pInfo = &ffi_info;
        }

        ddog_CharSlice* pProcessTags = nullptr;
        ddog_CharSlice ffi_processTags{};
        if (!processTags.empty())
        {
            ffi_processTags = to_char_slice(processTags);
            pProcessTags = &ffi_processTags;
        }

        auto result =
            ddog_prof_Exporter_send_blocking(
                &_exporter,
                profile,
                to_compress_files_view,
                static_cast<ddog_Vec_Tag const*>(*tags._impl),
                pProcessTags,
                pMetadata,
                pInfo,
                nullptr);

        if (result.tag == DDOG_PROF_RESULT_HTTP_STATUS_ERR_HTTP_STATUS)
        {
            return make_error(result.err);
        }

        if (IsErrorHttpCode(result.ok.code))
        {
            return make_error(std::to_string(result.ok.code));
        }

        return make_success();
    }

    bool IsErrorHttpCode(int16_t code)
    {
        // Although we expect only 200, we'll accept the whole range of valid codes
        return code < 200 || code >= 300;
    }

private:
    ddog_prof_ProfileExporter _exporter;
};
} // namespace libdatadog
