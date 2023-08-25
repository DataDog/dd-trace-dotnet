#include "aspect.h"
#include "../../../../shared/src/native-src/pal.h"
#include "../logger.h"
#include "iast_util.h"
#include "method_info.h"

namespace iast
{
BEGIN_ENUM_PARSE(AspectType)
ENUM_VALUE(AspectType, None)
ENUM_VALUE(AspectType, Source)
ENUM_VALUE(AspectType, Sink)
ENUM_VALUE(AspectType, Propagation)
END_ENUM_PARSE(AspectType)

BEGIN_ENUM_PARSE(VulnerabilityType)
ENUM_VALUE(VulnerabilityType, WeakCipher)
ENUM_VALUE(VulnerabilityType, WeakHash)
ENUM_VALUE(VulnerabilityType, SqlInjection)
ENUM_VALUE(VulnerabilityType, CommandInjection)
ENUM_VALUE(VulnerabilityType, PathTraversal)
ENUM_VALUE(VulnerabilityType, LdapInjection)
ENUM_VALUE(VulnerabilityType, Ssrf)
ENUM_VALUE(VulnerabilityType, UnvalidatedRedirect)
ENUM_VALUE(VulnerabilityType, InsecureCookie)
ENUM_VALUE(VulnerabilityType, NoHttpOnlyCookie)
ENUM_VALUE(VulnerabilityType, NoSameSiteCookie)
ENUM_VALUE(VulnerabilityType, None)
END_ENUM_PARSE(VulnerabilityType)

std::string ToString(const std::vector<VulnerabilityType> types)
{
    std::vector<std::string> texts;
    texts.reserve(types.size());
    for (auto type : types)
    {
        texts.push_back(ToString(type));
    }
    auto res = Join(texts);
    return res;
}
std::vector<VulnerabilityType> ParseVulnerabilityTypes(const std::string& txt)
{
    std::vector<VulnerabilityType> res;
    res.reserve(2);
    auto parts = Split(TrimEnd(TrimStart(txt, "["), "]"), ",");
    for (auto part : parts)
    {
        res.push_back(ParseVulnerabilityType(Trim(part)));
    }
    return res;
}

SpotInfo::SpotInfo(int line, Aspect* aspect, int id)
{
    this->_id = id;
    this->_line = line;
    this->_aspect = aspect;
    this->_lastChangedCheckEnabledStatus = GetStatus();
}
SpotInfo::~SpotInfo()
{
}
int SpotInfo::GetId()
{
    auto status = _lastChangedCheckEnabledStatus; // GetStatus();
    if (status == SpotInfoStatus::Disabled)
    {
        return -1;
    }
    if (status == SpotInfoStatus::Enabled)
    {
        return 0;
    }
    return _id;
}
SpotInfoStatus SpotInfo::GetStatus()
{
    if (_isUntracked)
    {
        return SpotInfoStatus::Enabled;
    }
    if (!_isMethodExcluded && (_isDisabled || !_aspect->IsEnabled()))
    {
        return SpotInfoStatus::Disabled;
    }
    return SpotInfoStatus::Tracked;
}

bool SpotInfo::HasChanged(const std::set<int>& untrackedSpotIds, const std::set<int>& disabledSpotIds,
                          bool methodExcludedFromRejit)
{
    if (!_isUntracked)
    {
        _isUntracked = untrackedSpotIds.find(_id) != untrackedSpotIds.end();
    }
    if (!_isDisabled)
    {
        _isDisabled = disabledSpotIds.find(_id) != disabledSpotIds.end();
    }
    _isMethodExcluded = methodExcludedFromRejit;
    auto newStatus = GetStatus();
    bool res = newStatus != _lastChangedCheckEnabledStatus;
    _lastChangedCheckEnabledStatus = newStatus;
    return res;
}

Aspect::Aspect()
{
}
Aspect::~Aspect()
{
}

int Aspect::GetSpotInfoId(MethodInfo* method, int line, mdMemberRef* aspectMemberRef)
{
    if (aspectMemberRef && IsEnabled())
    {
        *aspectMemberRef = GetAspectMemberRef();
        return 0;
    }
    else if (aspectMemberRef)
    {
        *aspectMemberRef = 0;
    }

    return -1;
}

bool Aspect::IsEnabled()
{
    auto aspectType = GetAspectType();
    if (aspectType != AspectType::Sink)
    {
        return true;
    }
    for (auto type : GetVulnerabilityTypes())
    {
        //TODO:  Check if any type is enabled in config
        //  auto key = ToLower(ToString(type));
        //  auto value = HdivConfig::Instance.GetEnabled(key + ".enabled", true) || HdivConfig::Instance.GetEnabled(key
        //  + ".attackEnabled", true); if (value)
        {
            return true;
        }
    }
    return false;
}
} // namespace iast
