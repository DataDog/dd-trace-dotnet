

extern "C"
{
#include "datadog/common.h"
#include "datadog/profiling.h"
}

struct EncodedProfile
{
    struct EncodedProfileDeleter
    {
        void operator()(ddog_prof_EncodedProfile* o)
        {
            ddog_prof_EncodedProfile_drop(o);
        }
    };

    using encoded_profile_ptr = std::unique_ptr<ddog_prof_EncodedProfile, EncodedProfileDeleter>;

    EncodedProfile(ddog_prof_EncodedProfile* p) :
        _profile(p)
    {
    }

    operator ddog_prof_EncodedProfile*()
    {
        return _profile.get();
    }

    encoded_profile_ptr _profile;
};
