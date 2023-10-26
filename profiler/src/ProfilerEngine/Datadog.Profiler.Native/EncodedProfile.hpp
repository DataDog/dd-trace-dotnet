// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <memory>

extern "C"
{
#include "datadog/common.h"
#include "datadog/profiling.h"
}

struct EncodedProfile
{
    EncodedProfile(ddog_prof_EncodedProfile* p) :
        _profile(p)
    {
    }

    struct EncodedProfileDeleter
    {
        void operator()(ddog_prof_EncodedProfile* o)
        {
            ddog_prof_EncodedProfile_drop(o);
        }
    };

    using encoded_profile_ptr = std::unique_ptr<ddog_prof_EncodedProfile, EncodedProfileDeleter>;


    operator ddog_prof_EncodedProfile*()
    {
        return _profile.get();
    }

    encoded_profile_ptr _profile;
};
