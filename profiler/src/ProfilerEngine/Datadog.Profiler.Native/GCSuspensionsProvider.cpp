// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "IConfiguration.h"
#include "GCSuspensionsProvider.h"
#include "OpSysTools.h"

#include <fstream>
#include <iostream>

SuspensionInfo::SuspensionInfo(int32_t number, uint32_t generation, uint64_t pauseDuration, uint64_t timestamp)
    :
    Number{number},
    Generation{generation},
    PauseDuration{pauseDuration},
    Timestamp{timestamp}
{
}


GCSuspensionsProvider::GCSuspensionsProvider(IConfiguration* configuration)
    :
    _pBuffer{nullptr},
    _current{1}

{
    _fileFolder = configuration->GetProfilesOutputDirectory();
    _suspensions.reserve(2048);
}

GCSuspensionsProvider::~GCSuspensionsProvider()
{
    if (_pBuffer != nullptr)
    {
        delete _pBuffer;
    }
}

bool GCSuspensionsProvider::GetSuspensions(uint8_t*& pBuffer, uint64_t& bufferSize)
{
    std::stringstream builder;
    builder << "[";

    bool isFirst = true;
    {
        // TODO: see how to use vector::swap for shorter lock
        std::lock_guard<std::mutex> lock(_suspensionsLock);
        for (auto const& suspension : _suspensions)
        {
            if (isFirst)
            {
                isFirst = false;
            }
            else
            {
                builder << ", ";
            }
            ToJson(builder, suspension);
        }
        _suspensions.clear();
    }

    builder << "]";

    auto content = builder.str();
    std::cout << content << std::endl;

    // ONLY for debug
    SaveSuspensions(content);

    return false;
}

void GCSuspensionsProvider::ToJson(std::stringstream& builder, const SuspensionInfo& suspension)
{
    builder << "{\"number\":" << suspension.Number;
    builder << ", \"generation\":" << suspension.Generation;
    builder << ", \"duration\":" << suspension.PauseDuration;
    builder << ", \"timestamp\":" << suspension.Timestamp;
    builder << "}";
}


void GCSuspensionsProvider::OnSuspension(int32_t number, uint32_t generation, uint64_t pauseDuration, uint64_t timestamp)
{
    std::stringstream builder;
    builder << timestamp << " | " << number << " - " << generation << " = " << pauseDuration << std::endl;
    std::cout << builder.str();

    std::lock_guard<std::mutex> lock(_suspensionsLock);
    _suspensions.push_back(SuspensionInfo(number, generation, pauseDuration, timestamp));
}

void GCSuspensionsProvider::SaveSuspensions(std::string& content)
{
    std::stringstream filename;
    filename << "timeline-" << std::to_string(OpSysTools::GetProcId()) << "." << _current++ << "." << "suspension.json";
    std::filesystem::path filepath = fs::path(_fileFolder) / filename.str();
    std::ofstream file{filepath.string(), std::ios::out | std::ios::binary};

    auto buffer = content.c_str();

    file.write(buffer, strlen(buffer));
    file.close();
}
