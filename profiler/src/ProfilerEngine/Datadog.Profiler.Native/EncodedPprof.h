// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "OpSysTools.h"

#include <chrono>
#include <cstdint>
#include <ctime>
#include <iomanip>
#include <map>
#include <sstream>
#include <string>
#include <vector>

// Holds the result of PprofBuilder::Serialize: the raw (uncompressed) pprof
// protobuf bytes plus the metadata needed to build the upload request
// (collection window and per-endpoint trace counts).
//
// Replaces the libdatadog EncodedProfile wrapper. The lazy GetId() keeps the
// same behavior used to name debug files written to disk.
class EncodedPprof
{
public:
    EncodedPprof() = default;

    std::vector<uint8_t> Bytes;
    std::chrono::system_clock::time_point Start{};
    std::chrono::system_clock::time_point End{};
    std::map<std::string, int64_t> EndpointCounts;

    // The id is used only when saving the file on disk, so its computation is lazy.
    std::string const& GetId()
    {
        if (_id.empty())
        {
            auto time = std::time(nullptr);
            struct tm buf = {};

#ifdef _WINDOWS
            localtime_s(&buf, &time);
#else
            localtime_r(&time, &buf);
#endif
            std::stringstream oss;
            oss << std::put_time(&buf, "%F_%H-%M-%S") << "_" << (OpSysTools::GetHighPrecisionNanoseconds() % 10000);
            _id = oss.str();
        }
        return _id;
    }

private:
    std::string _id;
};
