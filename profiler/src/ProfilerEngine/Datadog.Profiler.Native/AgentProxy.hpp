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

    Success Send(ddog_prof_EncodedProfile* profile, Tags tags, std::vector<std::pair<std::string, std::string>> files, std::string metadata, std::string info)
    {
        auto [request, ec] = CreateRequest(profile, std::move(tags), std::move(files), std::move(metadata), std::move(info));
        if (!ec)
        {
            return std::move(ec); // ?? really ?? otherwise it calls the copy constructor :sad:
        }

        assert(request != nullptr);

        auto result = ddog_prof_Exporter_send(&_exporter, request, nullptr);

        if (result.tag == DDOG_PROF_RESULT_HTTP_STATUS_ERR_HTTP_STATUS)
        {
            return make_error(result.err);
        }

        if (IsValidHttpCode(result.ok.code))
        {
            return make_error(std::to_string(result.ok.code));
        }

        return make_success();
    }

    bool IsValidHttpCode(int16_t code)
    {
        // Although we expect only 200, this range represents successful sends
        return code < 200 || code >= 300;
    }

private:
    struct Request
    {
        Request(ddog_prof_Request p) :
            _inner(p)
        {
        }

        Request(std::nullptr_t) :
            _inner{}
        {
        }

        ~Request()
        {
            ddog_prof_Exporter_Request_drop(&_inner);
        }

        Request(Request const&) = delete;
        Request& operator=(Request const&) = delete;

        Request(Request&& o) noexcept : _inner{}
        {
            *this = std::move(o);
        }

        Request& operator=(Request&& o) noexcept
        {
            if (this != &o)
            {
                std::swap(_inner, o._inner);
            }
            return *this;
        }

        operator ddog_prof_Request*()
        {
            return &_inner;
        }

    private:
        ddog_prof_Request _inner;
    };

    std::pair<Request, Success> CreateRequest(ddog_prof_EncodedProfile* encodedProfile, Tags&& tags, std::vector<std::pair<std::string, std::string>> files, std::string metadata, std::string info)
    {
        std::string const profile_filename = "auto.pprof";

        std::vector<ddog_prof_Exporter_File> to_compress_files;
        to_compress_files.reserve(files.size());

        for (auto& [filename, content] : files)
        {
            ddog_Slice_U8 fileSlice{reinterpret_cast<const uint8_t*>(content.c_str()), content.size()};
            to_compress_files.push_back({to_char_slice(filename), fileSlice});
        }

        auto uncompressed_files_view = ddog_prof_Exporter_Slice_File_empty();
        ddog_prof_Exporter_Slice_File to_compress_files_view = {to_compress_files.data(), to_compress_files.size()};

        ddog_CharSlice* pMetadata = nullptr;
        ddog_CharSlice ffi_metadata{};
        if (!metadata.empty())
        {
            ffi_metadata = to_char_slice(metadata);
            pMetadata = &ffi_metadata;
        }

        // json defined in internal RFC - Pprof System Info Support
        // that is used for SSI telemetry metrics.
        // Mostly already passed through tags today
        ddog_CharSlice* pInfo = nullptr;
        ddog_CharSlice ffi_info{};
        if (!info.empty())
        {
            ffi_info = to_char_slice(info);
            pInfo = &ffi_info;
        }

        auto requestResult =
            ddog_prof_Exporter_Request_build(
                &_exporter, encodedProfile,
                to_compress_files_view, uncompressed_files_view,
                static_cast<ddog_Vec_Tag const*>(*tags._impl),
                pMetadata, pInfo);

        if (requestResult.tag == DDOG_PROF_REQUEST_RESULT_ERR_HANDLE_REQUEST)
        {
            return std::make_pair(Request{nullptr}, make_error(requestResult.err));
        }
        return std::make_pair(Request{requestResult.ok}, make_success());
    }

private:
    ddog_prof_ProfileExporter _exporter;
};
} // namespace libdatadog
