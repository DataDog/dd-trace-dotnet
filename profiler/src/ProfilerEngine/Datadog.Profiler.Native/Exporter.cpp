// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "Exporter.h"

#include "AgentProxy.hpp"
#include "EncodedProfile.hpp"
#include "Exception.h"
#include "FfiHelper.h"
#include "FileSaver.hpp"
#include "Log.h"
#include "Profile.h"
#include "ProfileImpl.hpp"
#include "Tags.h"

#include <cassert>

namespace libdatadog {

Exporter::Exporter(std::unique_ptr<AgentProxy> agentProxy, std::unique_ptr<FileSaver> fileSaver) :
    _agentProxy{std::move(agentProxy)},
    _fileSaver{std::move(fileSaver)}
{
}

Exporter::~Exporter() = default;

libdatadog::Success Exporter::Send(Profile* profile, Tags tags, std::vector<std::pair<std::string, std::vector<uint8_t>>> files, std::string metadata, std::string info, std::string processTags)
{
    ddog_prof_encoded_profile* rawEncoded = nullptr;
    auto s = ddog_prof_profile_serialize(*profile->_impl, nullptr, nullptr, &rawEncoded);

    if (s != DDOG_OK)
    {
        return make_error(s);
    }

    auto ep = EncodedProfile(rawEncoded);
    if (_fileSaver != nullptr)
    {
        auto success = _fileSaver->WriteToDisk(ep, profile->GetApplicationName(), files, metadata, info);
        if (!success)
        {
            Log::Error(success.message());
        }
    }

    assert(_agentProxy != nullptr);
    return _agentProxy->Send(ep, std::move(tags), std::move(files), std::move(metadata), std::move(info), std::move(processTags));
}

} // namespace libdatadog
