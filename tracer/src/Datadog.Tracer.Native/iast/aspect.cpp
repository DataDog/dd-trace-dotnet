#include "aspect.h"
#include "../../../../shared/src/native-src/pal.h"
#include "../logger.h"
#include "iast_util.h"
#include "method_info.h"

namespace iast
{
BEGIN_ENUM_PARSE(AspectType)
ENUM_VALUE(AspectType, NONE)
ENUM_VALUE(AspectType, SOURCE)
ENUM_VALUE(AspectType, SINK)
ENUM_VALUE(AspectType, PROPAGATION)
END_ENUM_PARSE(AspectType)

BEGIN_ENUM_PARSE(VulnerabilityType)
ENUM_VALUE(VulnerabilityType, NONE)
ENUM_VALUE(VulnerabilityType, SQL_INJECTION)
ENUM_VALUE(VulnerabilityType, XSS)
ENUM_VALUE(VulnerabilityType, SSRF)
ENUM_VALUE(VulnerabilityType, ARBITRARY_SOCKET_CONNECTION)
ENUM_VALUE(VulnerabilityType, DEPENDENCY)
ENUM_VALUE(VulnerabilityType, UNTRUSTED_DESERIALIZATION)
ENUM_VALUE(VulnerabilityType, INSECURE_HASHING)
ENUM_VALUE(VulnerabilityType, INSECURE_CIPHER)
ENUM_VALUE(VulnerabilityType, WEAK_RANDOMNESS)
ENUM_VALUE(VulnerabilityType, UNVALIDATED_REDIRECT)
ENUM_VALUE(VulnerabilityType, CMD_INJECTION)
ENUM_VALUE(VulnerabilityType, ARBITRARY_CODE_EXECUTION)
ENUM_VALUE(VulnerabilityType, INSECURE_COOKIE)
ENUM_VALUE(VulnerabilityType, NO_HTTP_ONLY_COOKIE)
ENUM_VALUE(VulnerabilityType, PATH_TRAVERSAL)
ENUM_VALUE(VulnerabilityType, REFLECTION_INJECTION)
ENUM_VALUE(VulnerabilityType, TRUST_BOUNDARY_VIOLATION)
ENUM_VALUE(VulnerabilityType, HEADER_INJECTION)
ENUM_VALUE(VulnerabilityType, LDAP_INJECTION)
ENUM_VALUE(VulnerabilityType, SQL_INJECTION)
ENUM_VALUE(VulnerabilityType, XPATH_INJECTION)
ENUM_VALUE(VulnerabilityType, NO_SQL_INJECTION)
ENUM_VALUE(VulnerabilityType, SQL_INJECTION)
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
    res.reserve(5);
    for (auto v : Split(txt, ",; "))
    {
        auto e = ParseVulnerabilityType(v);
        if (e != VulnerabilityType::NONE)
        {
            res.push_back(e);
        }
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
    if (status == SpotInfoStatus::DISABLED)
    {
        return -1;
    }
    if (status == SpotInfoStatus::ENABLED)
    {
        return 0;
    }
    return _id;
}
SpotInfoStatus SpotInfo::GetStatus()
{
    if (_isUntracked)
    {
        return SpotInfoStatus::ENABLED;
    }
    if (!_isMethodExcluded && (_isDisabled || !_aspect->IsEnabled()))
    {
        return SpotInfoStatus::DISABLED;
    }
    return SpotInfoStatus::TRACKED;
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
    if (aspectType != AspectType::SINK)
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
