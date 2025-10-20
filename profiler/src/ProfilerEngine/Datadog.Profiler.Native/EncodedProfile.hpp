// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "OpSysTools.h"

#include <fstream>
#include <memory>

extern "C"
{
#include "datadog/common.h"
#include "datadog/profiling.h"
}

struct EncodedProfile
{
    EncodedProfile(ddog_prof_EncodedProfile p) :
        _profile(p)
    {
    }

    ~EncodedProfile()
    {
        ddog_prof_EncodedProfile_drop(&_profile);
    }

    operator ddog_prof_EncodedProfile*	()
    {
        return &_profile;
    }

    // The id is used only when saving file on disk, so make its computation lazy
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
        ddog_prof_EncodedProfile _profile;
    std::string _id;
};
