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
ENUM_VALUE(VulnerabilityType, None)
ENUM_VALUE(VulnerabilityType, SqlInjection)
ENUM_VALUE(VulnerabilityType, Xss)
ENUM_VALUE(VulnerabilityType, WeakCipher)
ENUM_VALUE(VulnerabilityType, WeakHash)
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
} // namespace iast
