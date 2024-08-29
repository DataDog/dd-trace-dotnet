#pragma once
#include "../../../../shared/src/native-src/pal.h"

namespace iast
{
    class ModuleInfo;
    class Dataflow;
    class MethodInfo;
    struct ILInstr;

    enum class AspectType
    {
        None,
        Source,
        Sink,
        Propagation
    };
    enum class VulnerabilityType
    {
        None,
        SqlInjection,
        Xss,
        WeakCipher,
        WeakHash
    };
    enum class SpotInfoStatus
    {
        Disabled,
        Enabled,
        Tracked
    };

    std::string ToString(AspectType type);
    AspectType ParseAspectType(const std::string& txt);
    std::string ToString(VulnerabilityType type);
    VulnerabilityType ParseVulnerabilityType(const std::string& txt);
    std::string ToString(std::vector<VulnerabilityType> types);
    std::vector<VulnerabilityType> ParseVulnerabilityTypes(const std::string& txt);
}