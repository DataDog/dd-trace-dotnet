#pragma once
#pragma once
#include "../../../../shared/src/native-src/pal.h"
#include <map>
using namespace shared;

namespace iast
{
    class AppDomainInfo
    {
    public:
        AppDomainID Id = 0;
        WSTRING Name = EmptyWStr;

        bool IsSharedAssemblyRepository = false;
        bool IsDefaultDomain = false;
        bool IsIisDomain = false;
        bool IsExcluded = false;

        AppDomainInfo() { }
        AppDomainInfo(AppDomainID appDomainId, WSTRING sName, bool isExcluded)
        {
            this->Id = appDomainId;
            this->Name = sName;

            this->IsSharedAssemblyRepository = IsValid() && (Name == WStr("EE Shared Assembly Repository"));
            this->IsDefaultDomain = IsValid() && (Name == WStr("DefaultDomain"));
            this->IsIisDomain = IsValid() && (Name.find(WStr("/LM/W3SVC/")) != std::wstring::npos);
            this->IsExcluded = !IsValid() || isExcluded;
        }

        bool IsValid() { return Id != 0; }
    };
}