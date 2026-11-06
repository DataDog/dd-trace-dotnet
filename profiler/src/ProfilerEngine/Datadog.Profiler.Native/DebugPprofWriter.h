// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "EncodedPprof.h"
#include "FfiHelper.h"
#include "FileHelper.h"
#include "Success.h"

#include "shared/src/native-src/dd_filesystem.hpp"

#include <cassert>
#include <cerrno>
#include <cstdint>
#include <cstring>
#include <fstream>
#include <sstream>
#include <string>
#include <utility>
#include <vector>

// Writes the raw (uncompressed) pprof plus its companion files to disk when a
// profiles output directory is configured (DD_PROFILING_OUTPUT_DIR). Replaces
// the libdatadog-based FileSaver: the profile bytes are already available in
// EncodedPprof, so there is no ddog_prof_EncodedProfile_bytes call.
class DebugPprofWriter
{
public:
    explicit DebugPprofWriter(fs::path outputDirectory) :
        _outputDirectory{std::move(outputDirectory)}
    {
    }

    libdatadog::Success WriteToDisk(
        EncodedPprof& profile,
        std::string const& serviceName,
        std::vector<std::pair<std::string, std::vector<uint8_t>>> const& files,
        std::string const& metadata,
        std::string const& info)
    {
        auto const& profileId = profile.GetId();

        bool hasError = false;
        std::stringstream errorMessage;

        auto success = WriteBinaryFileToDisk("", ".pprof", profile.Bytes, serviceName, profileId);
        if (!success)
        {
            hasError = true;
            errorMessage << success.message() << "\n";
        }

        for (auto const& [filename, content] : files)
        {
            auto [name, extension] = SplitFilenameAndExtension(filename);
            success = WriteBinaryFileToDisk(name, extension, content, serviceName, profileId);
            if (!success)
            {
                errorMessage << success.message() << "\n";
                hasError = true;
            }
        }

        if (!metadata.empty())
        {
            success = WriteTextFileToDisk("metadata.json", metadata, serviceName, profileId);
            if (!success)
            {
                errorMessage << success.message() << "\n";
                hasError = true;
            }
        }

        if (!info.empty())
        {
            success = WriteTextFileToDisk("info.json", info, serviceName, profileId);
            if (!success)
            {
                errorMessage << success.message() << "\n";
                hasError = true;
            }
        }

        if (hasError)
        {
            return libdatadog::make_error(errorMessage.str());
        }
        return libdatadog::make_success();
    }

private:
    libdatadog::Success WriteTextFileToDisk(const std::string& filenameWithExt, const std::string& content, std::string const& serviceName, std::string const& uid)
    {
        assert(fs::path(filenameWithExt).has_extension());

        auto [filename, extension] = SplitFilenameAndExtension(filenameWithExt);
        auto filepath = GenerateFilePath(filename, extension, serviceName, uid);

        return WriteFileToDisk(filepath, content.c_str(), content.size());
    }

    libdatadog::Success WriteBinaryFileToDisk(const std::string& filename, const std::string& extension, const std::vector<uint8_t>& content, std::string const& serviceName, std::string const& uid)
    {
        auto filepath = GenerateFilePath(filename, extension, serviceName, uid);
        return WriteFileToDisk(filepath, reinterpret_cast<const char*>(content.data()), content.size());
    }

    libdatadog::Success WriteFileToDisk(fs::path const& filePath, char const* ptr, std::size_t size)
    {
        std::ofstream file{filePath, std::ios::out | std::ios::binary};

        if (size != 0 && ptr != nullptr)
        {
            file.write(ptr, size);
        }
        file.close();

        if (file.fail())
        {
            char message[BufferMaxSize];
            auto errorCode = errno;
#ifdef _WINDOWS
            strerror_s(message, BufferMaxSize, errorCode);
#else
            strerror_r(errorCode, message, BufferMaxSize);
#endif
            return libdatadog::make_error(std::string("Unable to write file on disk: ") + filePath.string() + ". Message (code): " + message + " (" + std::to_string(errorCode) + ")");
        }

        return libdatadog::make_success();
    }

    static std::pair<std::string, std::string> SplitFilenameAndExtension(std::string const& filename)
    {
        fs::path file(filename);
        auto extension = file.extension();
        file.replace_extension();
        return {file.filename().string(), extension.string()};
    }

    fs::path GenerateFilePath(std::string const& filename, std::string const& extension, std::string const& serviceName, std::string const& uid) const
    {
        auto generatedFilename = FileHelper::GenerateFilename(filename, extension, serviceName, uid);
        return _outputDirectory / generatedFilename;
    }

    static constexpr std::size_t BufferMaxSize = 512;

    fs::path _outputDirectory;
};
