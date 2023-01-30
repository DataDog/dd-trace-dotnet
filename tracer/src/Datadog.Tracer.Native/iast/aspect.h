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
        NONE,
        SOURCE,
        SINK,
        PROPAGATION
    };
    enum class VulnerabilityType
    {
        NONE,
        SQL_INJECTION,
        XSS,
        SSRF,
        ARBITRARY_SOCKET_CONNECTION,
        DEPENDENCY,
        UNTRUSTED_DESERIALIZATION,
        INSECURE_HASHING,
        INSECURE_CIPHER,
        WEAK_RANDOMNESS,
        UNVALIDATED_REDIRECT,
        CMD_INJECTION,
        ARBITRARY_CODE_EXECUTION,
        INSECURE_COOKIE,
        NO_HTTP_ONLY_COOKIE,
        PATH_TRAVERSAL,
        REFLECTION_INJECTION,
        TRUST_BOUNDARY_VIOLATION,
        HEADER_INJECTION,
        LDAP_INJECTION,
        XPATH_INJECTION,
        NO_SQL_INJECTION,
    };
    enum class SpotInfoStatus
    {
        DISABLED,
        ENABLED,
        TRACKED
    };

    std::string ToString(AspectType type);
    AspectType ParseAspectType(const std::string& txt);
    std::string ToString(VulnerabilityType type);
    VulnerabilityType ParseVulnerabilityType(const std::string& txt);
    std::string ToString(std::vector<VulnerabilityType> types);
    std::vector<VulnerabilityType> ParseVulnerabilityTypes(const std::string& txt);

    class Aspect;
    class SpotInfo
    {
    private:
        int _id = 0;
        int _line = 0;
        Aspect* _aspect = nullptr;
        SpotInfoStatus _lastChangedCheckEnabledStatus = SpotInfoStatus::ENABLED;
        bool _isDisabled = false;
        bool _isUntracked = false;
        bool _isMethodExcluded = false;
    public:
        SpotInfo(int line, Aspect* aspect, int id);
        ~SpotInfo();

        int GetId();
        SpotInfoStatus GetStatus();
        bool HasChanged(const std::set<int>& untrackedSpotIds, const std::set<int>& disabledSpotIds, bool methodExcludedFromRejit);
    };

    class Aspect
    {
    public:
        Aspect();
        virtual ~Aspect();

        virtual std::string GetAspectTypeName() = 0;
        virtual std::string GetAspectMethodName() = 0;
        virtual AspectType GetAspectType() = 0;
        virtual std::vector<VulnerabilityType> GetVulnerabilityTypes() = 0;

        virtual mdMemberRef GetAspectMemberRef() = 0;

        int GetSpotInfoId(MethodInfo* method, int line, mdMemberRef* aspectMethodRef = nullptr);
        bool IsEnabled();
    };
}