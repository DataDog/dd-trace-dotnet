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

    class Aspect;
    class SpotInfo
    {
    private:
        int _id = 0;
        int _line = 0;
        Aspect* _aspect = nullptr;
        SpotInfoStatus _lastChangedCheckEnabledStatus = SpotInfoStatus::Enabled;
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